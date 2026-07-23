using System;
using System.Threading;
using System.Threading.Tasks;
using MavLinkSharp;
using MavLinkSharp.Connection;

namespace SignalFlux.Protocols.Mavlink
{
    /// <summary>Wraps a <see cref="MavLinkConnection"/> to subscribe to specific MAVLink message fields and forward extracted values via callbacks.</summary>
    public sealed class MavlinkConnectionAdapter : IAsyncDisposable
    {
        private readonly MavLinkConnection _connection;
        private bool _disposed;

        /// <summary>Creates an adapter wrapping the specified MAVLink connection.</summary>
        /// <param name="connection">The MavLinkSharp <see cref="MavLinkConnection"/> instance to wrap.</param>
        public MavlinkConnectionAdapter(MavLinkConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>Subscribes to a field from incoming MAVLink messages and invokes the handler with each value.</summary>
        /// <param name="messageId">The MAVLink message ID to subscribe to (e.g., 30 for ATTITUDE).</param>
        /// <param name="fieldName">The field name within the message to extract.</param>
        /// <param name="handler">The callback invoked with each extracted field value.</param>
        /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
        public IDisposable ObserveField(uint messageId, string fieldName, Action<double> handler)
        {
            _connection.OnMessage(messageId, frame =>
            {
                handler(frame.GetSingle(fieldName));
            });
            return new Subscription(() => { });
        }

        /// <summary>Connects the underlying MAVLink connection asynchronously.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return _connection.ConnectAsync(cancellationToken);
        }

        /// <summary>Disposes the adapter and releases the underlying MAVLink connection.</summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _unsubscribe;
            public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
            public void Dispose() => _unsubscribe?.Invoke();
        }
    }
}
