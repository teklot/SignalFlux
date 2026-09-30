using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalFlux.IO;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>
    /// An in-memory loopback SCPI connection. Bytes written by the client are buffered; when a complete
    /// command line (terminated by a newline) is assembled, an optional responder callback produces the
    /// instrument response bytes, which are then made available to subsequent reads. No real hardware is involved.
    /// </summary>
    public sealed class InMemoryScpiConnection : IStreamConnection
    {
        private readonly object _gate = new object();
        private readonly List<byte> _incoming = new List<byte>();
        private readonly List<byte> _outgoing = new List<byte>();
        private readonly Func<string, byte[]?>? _responder;
        private bool _disposed;

        /// <summary>Creates an isolated in-memory connection with an optional responder callback.</summary>
        /// <param name="responder">
        /// Callback that receives each complete command line (without the line terminator) and returns the
        /// exact response bytes to send back (including any required line terminator). When null, commands
        /// receive no reply. May be null.
        /// </param>
        public InMemoryScpiConnection(Func<string, byte[]?>? responder = null)
        {
            _responder = responder;
            Endpoint = new Uri("memory://scpi");
            Options = new ConnectionOptions();
            State = ConnectionState.Disconnected;
        }

        /// <inheritdoc/>
        public ConnectionState State { get; private set; }

        /// <inheritdoc/>
        public ConnectionOptions Options { get; }

        /// <inheritdoc/>
        public Uri Endpoint { get; }

        /// <summary>Number of complete command lines received but not yet removed from the write buffer.</summary>
        public int PendingCommandCount
        {
            get { lock (_gate) return CountLines(_outgoing); }
        }

        /// <summary>Number of response bytes queued and not yet read.</summary>
        public int QueuedResponseBytes
        {
            get { lock (_gate) return _incoming.Count; }
        }

        /// <inheritdoc/>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                State = ConnectionState.Connected;
                Monitor.PulseAll(_gate);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                State = ConnectionState.Disconnected;
                Monitor.PulseAll(_gate);
            }
            return Task.CompletedTask;
        }

        /// <summary>Reads up to <paramref name="buffer"/>.Length queued response bytes, blocking until bytes arrive, the connection closes, or the read is cancelled.</summary>
        /// <inheritdoc/>
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                using (cancellationToken.Register(() => { lock (_gate) Monitor.PulseAll(_gate); }))
                {
                    while (_incoming.Count == 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException(cancellationToken);
                        if (State != ConnectionState.Connected)
                            return Task.FromResult(0);
                        Monitor.Wait(_gate);
                    }
                }
                int count = Math.Min(buffer.Length, _incoming.Count);
                for (int i = 0; i < count; i++)
                    buffer.Span[i] = _incoming[i];
                _incoming.RemoveRange(0, count);
                return Task.FromResult(count);
            }
        }

        /// <summary>Buffers command bytes, invoking the responder for each complete line and queuing its reply.</summary>
        /// <inheritdoc/>
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (State != ConnectionState.Connected)
                    throw new InvalidOperationException("Connection is not connected.");
                for (int i = 0; i < data.Length; i++)
                    _outgoing.Add(data.Span[i]);
                ProcessPendingCommands();
                return Task.CompletedTask;
            }
        }

        /// <summary>Returns a stream that reads from and writes to this connection's buffers.</summary>
        /// <remarks>The returned stream is synchronous; reads return only response bytes already produced by the responder.</remarks>
        /// <inheritdoc/>
        public Stream GetStream()
        {
            lock (_gate) ThrowIfDisposed();
            return new InMemoryScpiStream(this);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                _disposed = true;
                _incoming.Clear();
                _outgoing.Clear();
                Monitor.PulseAll(_gate);
            }
            return default;
        }

        private void ProcessPendingCommands()
        {
            if (_responder == null || State != ConnectionState.Connected)
                return;
            while (true)
            {
                int terminator = _outgoing.IndexOf((byte)'\n');
                if (terminator < 0)
                    return;
                int lineLength = terminator;
                if (lineLength > 0 && _outgoing[lineLength - 1] == (byte)'\r')
                    lineLength--;
                string command = Encoding.ASCII.GetString(_outgoing.GetRange(0, lineLength).ToArray());
                _outgoing.RemoveRange(0, terminator + 1);
                byte[]? response = _responder(command);
                if (response == null)
                    return;
                _incoming.AddRange(response);
                Monitor.PulseAll(_gate);
            }
        }

        private static int CountLines(List<byte> buffer)
        {
            int count = 0;
            foreach (byte b in buffer)
                if (b == (byte)'\n')
                    count++;
            return count;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InMemoryScpiConnection));
        }

        private sealed class InMemoryScpiStream : Stream
        {
            private readonly InMemoryScpiConnection _owner;

            public InMemoryScpiStream(InMemoryScpiConnection owner)
            {
                _owner = owner;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                var target = buffer.AsMemory(offset, count);
                Task<int> read = _owner.ReadAsync(target, CancellationToken.None);
                return read.GetAwaiter().GetResult();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                var source = buffer.AsMemory(offset, count);
                Task write = _owner.WriteAsync(source, CancellationToken.None);
                write.GetAwaiter().GetResult();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}