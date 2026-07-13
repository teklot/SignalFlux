using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.IO
{
    /// <summary>UDP client connection implementing <see cref="IStreamConnection"/>.</summary>
    public sealed class UdpConnection : IStreamConnection
    {
        private UdpClient _client;
        private ConnectionState _state;
        private readonly int _localPort;

        /// <summary>The current state of the UDP connection.</summary>
        public ConnectionState State => _state;
        /// <summary>The configuration options for this connection.</summary>
        public ConnectionOptions Options { get; }
        /// <summary>The remote endpoint URI (udp://host:port).</summary>
        public Uri Endpoint { get; }

        /// <summary>Creates a UDP connection to the specified host and port.</summary>
        /// <param name="host">The remote hostname or IP address.</param>
        /// <param name="port">The remote port number.</param>
        /// <param name="localPort">Optional local port to bind to (0=ephemeral).</param>
        /// <param name="options">Optional connection configuration.</param>
        public UdpConnection(string host, int port, int localPort = 0, ConnectionOptions options = null)
        {
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"udp://{host}:{port}");
            _localPort = localPort;
        }

        /// <summary>Creates a UDP connection to the specified endpoint.</summary>
        /// <param name="endpoint">The remote IP endpoint.</param>
        /// <param name="localPort">Optional local port to bind to (0=ephemeral).</param>
        /// <param name="options">Optional connection configuration.</param>
        public UdpConnection(IPEndPoint endpoint, int localPort = 0, ConnectionOptions options = null)
        {
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"udp://{endpoint}");
            _localPort = localPort;
        }

        /// <summary>Opens the UDP socket and connects to the remote endpoint.</summary>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
                return Task.CompletedTask;

            _state = ConnectionState.Connecting;

            try
            {
                _client = new UdpClient(_localPort);
                _client.Client.ReceiveBufferSize = Options.ReceiveBufferSize;
                _client.Client.SendBufferSize = Options.SendBufferSize;
                _client.Connect(Endpoint.Host, Endpoint.Port);
                _state = ConnectionState.Connected;
            }
            catch
            {
                _state = ConnectionState.Disconnected;
                _client?.Dispose();
                _client = null;
                throw;
            }

            return Task.CompletedTask;
        }

        /// <summary>Closes the UDP socket.</summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectionState.Disconnecting;
            _client?.Dispose();
            _client = null;
            _state = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        /// <summary>Receives a datagram from the remote endpoint.</summary>
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.ReadTimeout);

#if NET10_0
            var result = await _client.ReceiveAsync(cts.Token).ConfigureAwait(false);
#else
            var result = await _client.ReceiveAsync().ConfigureAwait(false);
#endif
            var data = result.Buffer;
            int count = Math.Min(data.Length, buffer.Length);
            data.AsMemory(0, count).CopyTo(buffer);
            return count;
        }

        /// <summary>Sends a datagram to the remote endpoint.</summary>
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.WriteTimeout);
            await _client.SendAsync(data.ToArray(), data.Length).ConfigureAwait(false);
        }

        /// <summary>Returns a stream wrapper for the UDP socket.</summary>
        public Stream GetStream()
        {
            ThrowIfNotConnected();
            return new UdpStreamWrapper(this);
        }

        /// <summary>Disposes the UDP connection asynchronously.</summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        private void ThrowIfNotConnected()
        {
            if (_state != ConnectionState.Connected)
                throw new InvalidOperationException($"Connection is not connected. Current state: {_state}");
        }

        private sealed class UdpStreamWrapper : Stream
        {
            private readonly UdpConnection _conn;

            public UdpStreamWrapper(UdpConnection conn) => _conn = conn;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) =>
                _conn.ReadAsync(new Memory<byte>(buffer, offset, count)).GetAwaiter().GetResult();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                _conn.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count)).GetAwaiter().GetResult();
        }
    }
}
