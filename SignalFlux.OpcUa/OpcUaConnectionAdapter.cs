using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

namespace SignalFlux.Protocols.OpcUa
{
    /// <summary>Represents a node in the OPC UA address space.</summary>
    public sealed class NodeInfo
    {
        /// <summary>The node identifier string (e.g., "ns=2;s=Temperature").</summary>
        public string NodeId { get; }

        /// <summary>The display name of the node.</summary>
        public string DisplayName { get; }

        /// <summary>The node class (Variable, Object, Method, etc.).</summary>
        public NodeClass NodeClass { get; }

        /// <summary>Creates a new node info.</summary>
        public NodeInfo(string nodeId, string displayName, NodeClass nodeClass)
        {
            NodeId = nodeId;
            DisplayName = displayName;
            NodeClass = nodeClass;
        }

        /// <summary>Returns a string representation.</summary>
        public override string ToString() => $"{DisplayName} ({NodeId}) [{NodeClass}]";
    }

    /// <summary>Wraps an OPC UA <see cref="ISession"/> to read, subscribe, and browse node values as SignalFlux <see cref="Measurement{T}"/>.</summary>
    public sealed class OpcUaConnectionAdapter : IAsyncDisposable
    {
        private readonly ISession _session;
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private bool _disposed;

        private OpcUaConnectionAdapter(ISession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>Connects to an OPC UA server and returns an adapter.</summary>
        /// <param name="serverUrl">The OPC UA server URL (e.g., "opc.tcp://localhost:4840").</param>
        /// <param name="applicationName">Application name for the OPC UA client.</param>
        /// <param name="useSecurity">Whether to use security (default false for anonymous).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A connected <see cref="OpcUaConnectionAdapter"/>.</returns>
        public static async Task<OpcUaConnectionAdapter> ConnectAsync(
            string serverUrl,
            string applicationName = "SignalFlux",
            bool useSecurity = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new ArgumentException("Server URL cannot be null or empty.", nameof(serverUrl));

            var config = new ApplicationConfiguration
            {
                ApplicationName = applicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationUri = $"urn:localhost:SignalFlux:{applicationName}",
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "%LocalApplicationData%/SignalFlux/pki/own",
                        SubjectName = applicationName,
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "%LocalApplicationData%/SignalFlux/pki/issuers",
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "%LocalApplicationData%/SignalFlux/pki/trusted",
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "%LocalApplicationData%/SignalFlux/pki/rejected",
                    },
                },
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 15_000,
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60_000,
                },
            };

            await config.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);

            config.CertificateValidator.CertificateValidation += (sender, e) =>
            {
                if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
                    e.Accept = true;
            };

            EndpointDescription endpointDescription = await CoreClientUtils
                .SelectEndpointAsync(config, serverUrl, useSecurity, telemetry: null, ct)
                .ConfigureAwait(false);

            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

            var sessionFactory = new DefaultSessionFactory(telemetry: null);
            ISession session = await sessionFactory.CreateAsync(
                config,
                configuredEndpoint,
                false,
                false,
                applicationName,
                60_000,
                (IUserIdentity)null,
                (IList<string>)null,
                ct).ConfigureAwait(false);

            return new OpcUaConnectionAdapter(session);
        }

        /// <summary>Reads a single OPC UA node and returns it as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="nodeId">The node identifier (e.g., "ns=2;s=Temperature").</param>
        /// <param name="source">Source identifier for the measurement.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="Measurement{T}"/> with the node's value, timestamp, and quality.</returns>
        public async Task<Measurement<double>> ReadNodeAsync(
            string nodeId,
            string source = "opcua",
            CancellationToken ct = default)
        {
            var id = NodeId.Parse(nodeId);
            DataValue dataValue = await _session.ReadValueAsync(id, ct).ConfigureAwait(false);
            return dataValue.ToMeasurement(source);
        }

        /// <summary>Reads multiple OPC UA nodes and returns them as measurements.</summary>
        /// <param name="nodeIds">The node identifiers to read.</param>
        /// <param name="source">Source identifier for the measurements.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of <see cref="Measurement{T}"/> values, one per node.</returns>
        public async Task<IReadOnlyList<Measurement<double>>> ReadNodesAsync(
            IEnumerable<string> nodeIds,
            string source = "opcua",
            CancellationToken ct = default)
        {
            var idArray = nodeIds.Select(NodeId.Parse).ToArray();
            var readValues = new ReadValueIdCollection(
                idArray.Select(id => new ReadValueId { NodeId = id, AttributeId = Attributes.Value }));

            ReadResponse response = await _session.ReadAsync(
                null, 0, TimestampsToReturn.Both, readValues, ct).ConfigureAwait(false);

            return response.Results.Select(dv => dv.ToMeasurement(source)).ToList();
        }

        /// <summary>Subscribes to live data changes on a node. The handler is invoked on each notification.</summary>
        /// <param name="nodeId">The node identifier to subscribe to.</param>
        /// <param name="handler">Callback invoked with the new measurement value.</param>
        /// <param name="samplingIntervalMs">Sampling interval in milliseconds (default 1000).</param>
        /// <param name="source">Source identifier for the measurement.</param>
        /// <returns>A disposable subscription that can be disposed to unsubscribe.</returns>
        public IDisposable SubscribeToNode(
            string nodeId,
            Action<Measurement<double>> handler,
            int samplingIntervalMs = 1000,
            string source = "opcua")
        {
            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = samplingIntervalMs,
                PublishingEnabled = true,
                KeepAliveCount = 5,
                MinLifetimeInterval = 60_000
            };

            _session.AddSubscription(subscription);

            var item = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value,
                DisplayName = nodeId,
                SamplingInterval = samplingIntervalMs,
                QueueSize = 10,
                DiscardOldest = true
            };

            item.Notification += (sender, e) =>
            {
                if (e.NotificationValue is MonitoredItemNotification notification)
                {
                    Measurement<double> measurement = notification.Value.ToMeasurement(source);
                    handler(measurement);
                }
            };

            subscription.AddItem(item);

            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }

            return new SubscriptionHandle(subscription, _subscriptions);
        }

        /// <summary>Browses the OPC UA address space starting from a given node.</summary>
        /// <param name="startNodeId">The starting node ID (default: root folder).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of child nodes.</returns>
        public async Task<IReadOnlyList<NodeInfo>> BrowseAsync(
            string startNodeId = null,
            CancellationToken ct = default)
        {
            NodeId rootId = startNodeId != null
                ? NodeId.Parse(startNodeId)
                : ObjectIds.RootFolder;

            var browser = new Browser(_session)
            {
                BrowseDirection = BrowseDirection.Forward,
                NodeClassMask = (int)(NodeClass.Variable | NodeClass.Object | NodeClass.ObjectType),
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true
            };

            ReferenceDescriptionCollection references = await browser.BrowseAsync(rootId, ct).ConfigureAwait(false);

            return references.Select(r => new NodeInfo(
                r.NodeId.ToString(),
                r.DisplayName.Text,
                r.NodeClass)).ToList();
        }

        /// <summary>Disposes the adapter and closes the OPC UA session.</summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                Subscription[] snapshot;
                lock (_subscriptions)
                {
                    snapshot = _subscriptions.ToArray();
                    _subscriptions.Clear();
                }

                foreach (var sub in snapshot)
                {
                    await sub.DeleteAsync(silent: true).ConfigureAwait(false);
                }

                await _session.CloseAsync().ConfigureAwait(false);
                _session.Dispose();
            }
        }

        private sealed class SubscriptionHandle : IDisposable
        {
            private readonly Subscription _subscription;
            private readonly List<Subscription> _owner;

            public SubscriptionHandle(Subscription subscription, List<Subscription> owner)
            {
                _subscription = subscription;
                _owner = owner;
            }

            public void Dispose()
            {
#pragma warning disable CS0618
                _subscription.Delete(silent: true);
#pragma warning restore CS0618
                lock (_owner)
                {
                    _owner.Remove(_subscription);
                }
            }
        }
    }
}
