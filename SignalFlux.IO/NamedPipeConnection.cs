using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.IO
{
    /// <summary>Named pipe client connection implementing <see cref="IStreamConnection"/>.</summary>
    public sealed class NamedPipeConnection : IStreamConnection
    {
        private NamedPipeClientStream _pipe;
        private ConnectionState _state;

        /// <summary>The current state of the pipe connection.</summary>
        public ConnectionState State => _state;
        /// <summary>The configuration options for this connection.</summary>
        public ConnectionOptions Options { get; }
        /// <summary>The endpoint URI (pipe://server/pipeName).</summary>
        public Uri Endpoint { get; }
        /// <summary>The name of the pipe.</summary>
        public string PipeName { get; }
        /// <summary>The server name ("." for local machine).</summary>
        public string ServerName { get; }

        /// <summary>Creates a named pipe connection to the specified pipe on the specified server.</summary>
        /// <param name="pipeName">The pipe name.</param>
        /// <param name="serverName">The server name ("." for local machine, default).</param>
        /// <param name="options">Optional connection configuration.</param>
        public NamedPipeConnection(string pipeName, string serverName = ".", ConnectionOptions options = null)
        {
            PipeName = pipeName;
            ServerName = serverName;
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"pipe://{serverName}/{pipeName}");
        }

        /// <summary>Connects to the named pipe server asynchronously.</summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
                return;

            _state = ConnectionState.Connecting;

            try
            {
                _pipe = new NamedPipeClientStream(ServerName, PipeName, PipeDirection.InOut);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(Options.ConnectTimeout);
                await _pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                _state = ConnectionState.Connected;
            }
            catch
            {
                _state = ConnectionState.Disconnected;
                _pipe?.Dispose();
                _pipe = null;
                throw;
            }
        }

        /// <summary>Disconnects the named pipe connection.</summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectionState.Disconnecting;
            _pipe?.Dispose();
            _pipe = null;
            _state = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        /// <summary>Reads data from the pipe into the provided buffer.</summary>
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
#if NET10_0
            return await _pipe.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
            var arr = new byte[buffer.Length];
            int count = await _pipe.ReadAsync(arr, 0, arr.Length, cancellationToken).ConfigureAwait(false);
            arr.AsMemory(0, count).CopyTo(buffer);
            return count;
#endif
        }

        /// <summary>Writes data to the pipe.</summary>
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
#if NET10_0
            await _pipe.WriteAsync(data, cancellationToken).ConfigureAwait(false);
#else
            await _pipe.WriteAsync(data.ToArray(), 0, data.Length, cancellationToken).ConfigureAwait(false);
#endif
        }

        /// <summary>Returns the underlying <see cref="NamedPipeClientStream"/>.</summary>
        public Stream GetStream()
        {
            ThrowIfNotConnected();
            return _pipe;
        }

        /// <summary>Disposes the pipe connection asynchronously.</summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        private void ThrowIfNotConnected()
        {
            if (_state != ConnectionState.Connected)
                throw new InvalidOperationException($"Connection is not connected. Current state: {_state}");
        }
    }
}
