using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// An in-memory loopback CAN transport suitable for testing and simulation. Frames sent by one
    /// instance are delivered to itself and to any peer it is linked with, and can be queued for
    /// explicit reads. No real hardware is involved.
    /// </summary>
    public sealed class InMemoryCanTransport : ICanTransport
    {
        private readonly ConcurrentQueue<CanFrame> _queue = new ConcurrentQueue<CanFrame>();
        private readonly object _gate = new object();
        private bool _open;
        private bool _disposed;

        /// <summary>Creates an isolated in-memory transport.</summary>
        public InMemoryCanTransport()
        {
        }

        /// <inheritdoc/>
        public event EventHandler<CanFrameReceivedEventArgs> FrameReceived;

        /// <inheritdoc/>
        public Task OpenAsync(CancellationToken ct = default)
        {
            lock (_gate) _open = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task CloseAsync()
        {
            lock (_gate) _open = false;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            lock (_gate)
            {
                if (!_open) throw new InvalidOperationException("Transport is not open.");
                _queue.Enqueue(frame);
            }
            FrameReceived?.Invoke(this, new CanFrameReceivedEventArgs(frame));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<CanFrame> ReadAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            lock (_gate)
            {
                if (_queue.TryDequeue(out CanFrame frame))
                    return Task.FromResult(frame);
                if (!_open) throw new InvalidOperationException("Transport is not open.");
            }
            return Task.FromResult(default(CanFrame));
        }

        /// <summary>Number of frames currently queued and not yet read.</summary>
        public int QueuedCount
        {
            get { lock (_gate) return _queue.Count; }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InMemoryCanTransport));
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }
    }
}
