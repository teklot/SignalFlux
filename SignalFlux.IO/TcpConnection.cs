using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.IO
{
    /// <summary>TCP client connection implementing <see cref="IStreamConnection"/>.</summary>
    public sealed class TcpConnection : IStreamConnection
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private ConnectionState _state;

        /// <summary>The current state of the TCP connection.</summary>
        public ConnectionState State => _state;
        /// <summary>The configuration options for this connection.</summary>
        public ConnectionOptions Options { get; }
        /// <summary>The remote endpoint URI (tcp://host:port).</summary>
        public Uri Endpoint { get; }

        /// <summary>Creates a TCP connection to the specified host and port.</summary>
        /// <param name="host">The remote hostname or IP address.</param>
        /// <param name="port">The remote port number.</param>
        /// <param name="options">Optional connection configuration.</param>
        public TcpConnection(string host, int port, ConnectionOptions options = null)
        {
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"tcp://{host}:{port}");
        }

        /// <summary>Creates a TCP connection to the specified endpoint.</summary>
        /// <param name="endpoint">The remote IP endpoint.</param>
        /// <param name="options">Optional connection configuration.</param>
        public TcpConnection(IPEndPoint endpoint, ConnectionOptions options = null)
        {
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"tcp://{endpoint}");
        }

        /// <summary>Connects to the remote TCP endpoint asynchronously.</summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
                return;

            _state = ConnectionState.Connecting;

            try
            {
                _client = new TcpClient();
                _client.NoDelay = Options.NoDelay;
                _client.ReceiveBufferSize = Options.ReceiveBufferSize;
                _client.SendBufferSize = Options.SendBufferSize;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(Options.ConnectTimeout);

                await _client.ConnectAsync(Endpoint.Host, Endpoint.Port).ConfigureAwait(false);

                _stream = _client.GetStream();
                _state = ConnectionState.Connected;
            }
            catch
            {
                _state = ConnectionState.Disconnected;
                _client?.Dispose();
                _client = null;
                throw;
            }
        }

        /// <summary>Disconnects and cleans up the TCP connection.</summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectionState.Disconnecting;
            _stream?.Dispose();
            _client?.Dispose();
            _stream = null;
            _client = null;
            _state = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        /// <summary>Reads data from the TCP stream into the provided buffer.</summary>
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.ReadTimeout);
#if NET10_0
            return await _stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
#else
            var arr = buffer.ToArray();
            int count = await _stream.ReadAsync(arr, 0, arr.Length, cts.Token).ConfigureAwait(false);
            arr.AsMemory(0, count).CopyTo(buffer);
            return count;
#endif
        }

        /// <summary>Writes data to the TCP stream.</summary>
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.WriteTimeout);
#if NET10_0
            await _stream.WriteAsync(data, cts.Token).ConfigureAwait(false);
#else
            await _stream.WriteAsync(data.ToArray(), 0, data.Length, cts.Token).ConfigureAwait(false);
#endif
        }

        /// <summary>Returns the underlying <see cref="NetworkStream"/>.</summary>
        public Stream GetStream()
        {
            ThrowIfNotConnected();
            return _stream;
        }

#if NET10_0
        /// <summary>Disposes the TCP connection asynchronously.</summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
#else
        /// <summary>Disposes the TCP connection.</summary>
        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
#endif

        private void ThrowIfNotConnected()
        {
            if (_state != ConnectionState.Connected)
                throw new InvalidOperationException($"Connection is not connected. Current state: {_state}");
        }
    }
}
