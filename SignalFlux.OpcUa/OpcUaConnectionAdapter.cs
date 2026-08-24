using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

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

    /// <summary>
    /// Wraps an OPC UA <see cref="ISession"/> to read, write, subscribe, and browse node values as SignalFlux
    /// <see cref="Measurement{T}"/> values. Supports anonymous and username/password authentication, application
    /// certificate creation, automatic reconnection, and engineering-unit resolution.
    /// </summary>
    public sealed class OpcUaConnectionAdapter : IAsyncDisposable
    {
        private readonly OpcUaConnectionOptions _options;
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly object _stateLock = new object();
        private ISession _session;
        private SessionReconnectHandler _reconnectHandler;
        private bool _disposed;
        private OpcUaConnectionState _state = OpcUaConnectionState.Connecting;

        private OpcUaConnectionAdapter(ISession session, OpcUaConnectionOptions options)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _session.KeepAlive += OnSessionKeepAlive;
            SetState(OpcUaConnectionState.Connected);
        }

        /// <summary>The underlying OPC UA session.</summary>
        internal ISession Session => _session;

        /// <summary>Gets the current connection state.</summary>
        public OpcUaConnectionState State
        {
            get { lock (_stateLock) return _state; }
        }

        /// <summary>Raised whenever the connection state changes (e.g., Connected → Reconnecting after a network drop).</summary>
        public event EventHandler<OpcUaConnectionStateChangedEventArgs> OnStateChanged;

        /// <summary>Connects to an OPC UA server using explicit connection options.</summary>
        /// <param name="serverUrl">The OPC UA server URL (e.g., "opc.tcp://localhost:4840").</param>
        /// <param name="options">Connection options; null uses all defaults (anonymous, no security).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A connected <see cref="OpcUaConnectionAdapter"/>.</returns>
        public static async Task<OpcUaConnectionAdapter> ConnectAsync(
            string serverUrl,
            OpcUaConnectionOptions options,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new ArgumentException("Server URL cannot be null or empty.", nameof(serverUrl));

            var opts = options ?? new OpcUaConnectionOptions();
            string appName = opts.ApplicationName ?? "SignalFlux";

            var config = new ApplicationConfiguration
            {
                ApplicationName = appName,
                ApplicationType = ApplicationType.Client,
                ApplicationUri = $"urn:localhost:SignalFlux:{appName}",
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = opts.AutoAcceptUntrustedCertificates,
                    RejectSHA1SignedCertificates = false,
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "%LocalApplicationData%/SignalFlux/pki/own",
                        SubjectName = appName,
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
                    OperationTimeout = opts.OperationTimeoutMs,
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = opts.SessionTimeoutMs,
                },
            };

            await config.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);

            if (opts.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (sender, e) =>
                {
                    if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
                        e.Accept = true;
                };
            }

            if (opts.CreateApplicationCertificate)
            {
                var application = new ApplicationInstance(telemetry: null)
                {
                    ApplicationName = appName,
                    ApplicationType = ApplicationType.Client,
                    ApplicationConfiguration = config,
                };

                await application.CheckApplicationInstanceCertificatesAsync(true, null, ct).ConfigureAwait(false);
            }

            EndpointDescription endpointDescription = await CoreClientUtils
                .SelectEndpointAsync(config, serverUrl, opts.UseSecurity, telemetry: null, ct)
                .ConfigureAwait(false);

            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

            IUserIdentity identity = BuildIdentity(opts.UserCredentials);
            uint sessionTimeout = (uint)opts.SessionTimeoutMs;

            var sessionFactory = new DefaultSessionFactory(telemetry: null);
            ISession session = await sessionFactory.CreateAsync(
                config,
                configuredEndpoint,
                false,
                false,
                appName,
                sessionTimeout,
                identity,
                (IList<string>)null,
                ct).ConfigureAwait(false);

            return new OpcUaConnectionAdapter(session, opts);
        }

        /// <summary>Connects to an OPC UA server anonymously with simple defaults (legacy overload).</summary>
        /// <param name="serverUrl">The OPC UA server URL.</param>
        /// <param name="applicationName">Application name for the OPC UA client.</param>
        /// <param name="useSecurity">Whether to use message security.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A connected <see cref="OpcUaConnectionAdapter"/>.</returns>
        public static Task<OpcUaConnectionAdapter> ConnectAsync(
            string serverUrl,
            string applicationName = "SignalFlux",
            bool useSecurity = false,
            CancellationToken ct = default)
        {
            return ConnectAsync(serverUrl, new OpcUaConnectionOptions
            {
                ApplicationName = applicationName,
                UseSecurity = useSecurity,
            }, ct);
        }

        private static IUserIdentity BuildIdentity(OpcUaUserCredentials credentials)
        {
            if (credentials == null || string.IsNullOrEmpty(credentials.UserName))
                return new UserIdentity(new AnonymousIdentityToken());

            return new UserIdentity(
                credentials.UserName,
                Encoding.UTF8.GetBytes(credentials.Password ?? string.Empty));
        }

        // ------------------------------------------------------------------
        // Write
        // ------------------------------------------------------------------

        /// <summary>Writes a value to a single OPC UA node.</summary>
        /// <param name="nodeId">The node identifier to write.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="ServiceResultException">Thrown when the server rejects the write.</exception>
        public async Task WriteNodeAsync(
            string nodeId,
            double value,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));

            var collection = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = NodeId.Parse(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value)),
                }
            };

            WriteResponse response = await _session.WriteAsync(null, collection, ct).ConfigureAwait(false);

            StatusCode status = response.Results.FirstOrDefault();
            if (StatusCode.IsBad(status.Code))
                throw new ServiceResultException(status.Code);
        }

        /// <summary>Writes values to multiple OPC UA nodes in a single batch call.</summary>
        /// <param name="writes">Key/value pairs of node identifier and value.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The per-node result statuses, in the same order as <paramref name="writes"/>.</returns>
        public async Task<IReadOnlyList<StatusCode>> WriteNodesAsync(
            IEnumerable<KeyValuePair<string, double>> writes,
            CancellationToken ct = default)
        {
            if (writes == null) throw new ArgumentNullException(nameof(writes));

            var items = writes.Select(kv => new WriteValue
            {
                NodeId = NodeId.Parse(kv.Key),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(kv.Value)),
            }).ToArray();

            if (items.Length == 0) return Array.Empty<StatusCode>();

            var collection = new WriteValueCollection(items);
            WriteResponse response = await _session.WriteAsync(null, collection, ct).ConfigureAwait(false);

            return response.Results.ToList();
        }

        // ------------------------------------------------------------------
        // Read
        // ------------------------------------------------------------------

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

        /// <summary>
        /// Reads a single OPC UA node together with its EngineeringUnit property. When the server reports a unit
        /// that maps to a known UnitsNet enum, the measurement carries the typed unit; otherwise the raw unit
        /// symbol is stored in metadata under the key "eu".
        /// </summary>
        /// <param name="nodeId">The node identifier to read.</param>
        /// <param name="source">Source identifier for the measurement.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="Measurement{T}"/> with unit information when available.</returns>
        public async Task<Measurement<double>> ReadNodeWithUnitAsync(
            string nodeId,
            string source = "opcua",
            CancellationToken ct = default)
        {
            var id = NodeId.Parse(nodeId);

            DataValue dataValue = await _session.ReadValueAsync(id, ct).ConfigureAwait(false);
            Measurement<double> measurement = dataValue.ToMeasurement(source);

            EUInformation eu = await ReadEngineeringUnitAsync(id, ct).ConfigureAwait(false);
            if (eu == null) return measurement;

            Enum typedUnit = OpcUaUnitMapper.TryGetUnit(eu.DisplayName?.Text)
                             ?? OpcUaUnitMapper.TryGetUnit(eu.Description?.Text);

            Metadata metadata = measurement.Metadata.With("eu", eu.DisplayName?.Text ?? eu.Description?.Text ?? "unknown");
            measurement = typedUnit != null
                ? measurement.WithUnit(typedUnit).WithMetadata(metadata)
                : measurement.WithMetadata(metadata);

            return measurement;
        }

        private async Task<EUInformation> ReadEngineeringUnitAsync(NodeId id, CancellationToken ct)
        {
            try
            {
                var browser = new Browser(_session)
                {
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HasProperty,
                    NodeClassMask = (int)NodeClass.Variable,
                    IncludeSubtypes = true,
                };

                ReferenceDescriptionCollection references = await browser.BrowseAsync(id, ct).ConfigureAwait(false);
                ReferenceDescription euRef = references.FirstOrDefault(r => r.BrowseName?.Name == "EngineeringUnit");
                if (euRef == null) return null;

                var propertyId = ExpandedNodeId.ToNodeId(euRef.NodeId, _session.NamespaceUris);
                DataValue propertyValue = await _session.ReadValueAsync(propertyId, ct).ConfigureAwait(false);

                if (!(propertyValue.WrappedValue.Value is ExtensionObject ext)) return null;

                object body = ext.Body;
                if (body is EUInformation direct) return direct;

#pragma warning disable CS0618
                return ExtensionObject.ToEncodeable(ext) as EUInformation;
#pragma warning restore CS0618
            }
            catch (ServiceResultException)
            {
                return null;
            }
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

        // ------------------------------------------------------------------
        // Subscribe
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // Browse
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // Reconnection
        // ------------------------------------------------------------------

        private void OnSessionKeepAlive(ISession sender, KeepAliveEventArgs e)
        {
            if (_disposed) return;
            if (e.Status == null || ServiceResult.IsGood(e.Status)) return;

            SetState(OpcUaConnectionState.Reconnecting);

            lock (_stateLock)
            {
                if (_reconnectHandler != null) return;
                _reconnectHandler = new SessionReconnectHandler(null);
            }

            _reconnectHandler.BeginReconnect(sender, _options.ReconnectPeriodMs, OnReconnectComplete);
        }

        private void OnReconnectComplete(object sender, EventArgs e)
        {
            var handler = sender as SessionReconnectHandler;
            if (handler == null || _disposed) return;

            // Per OPC UA stack contract: only adopt the session when the handler actually produced one.
            ISession newSession = handler.Session;
            if (newSession == null) return;

            ISession oldSession;
            lock (_stateLock)
            {
                oldSession = _session;
                _session = newSession;
                oldSession?.KeepAlive -= OnSessionKeepAlive;
                newSession.KeepAlive += OnSessionKeepAlive;
                _reconnectHandler = null;
                SetState(OpcUaConnectionState.Connected);
            }

            oldSession?.Dispose();
            handler.Dispose();
        }

        private void SetState(OpcUaConnectionState newState)
        {
            OpcUaConnectionStateChangedEventArgs args;
            lock (_stateLock)
            {
                if (_state == newState) return;
                var previous = _state;
                _state = newState;
                args = new OpcUaConnectionStateChangedEventArgs(previous, newState);
            }

            OnStateChanged?.Invoke(this, args);
        }

        // ------------------------------------------------------------------
        // Disposal
        // ------------------------------------------------------------------

        /// <summary>Disposes the adapter, deletes subscriptions, closes the session, and stops reconnection handling.</summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                SessionReconnectHandler reconnectHandler;
                lock (_stateLock)
                {
                    reconnectHandler = _reconnectHandler;
                    _reconnectHandler = null;
                }

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

                _session.KeepAlive -= OnSessionKeepAlive;

                if (reconnectHandler != null)
                {
                    reconnectHandler.Dispose();
                }

                await _session.CloseAsync().ConfigureAwait(false);
                _session.Dispose();

                SetState(OpcUaConnectionState.Disconnected);
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
