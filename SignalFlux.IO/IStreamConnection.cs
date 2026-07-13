using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.IO
{
    /// <summary>Unified abstraction for duplex stream-based connections over TCP, UDP, Serial, or Named Pipes.</summary>
    public interface IStreamConnection : IAsyncDisposable
    {
        /// <summary>The current state of the connection.</summary>
        ConnectionState State { get; }
        /// <summary>The configuration options for this connection.</summary>
        ConnectionOptions Options { get; }
        /// <summary>The remote endpoint URI (e.g., tcp://host:port, serial://COM1?baud=115200).</summary>
        Uri Endpoint { get; }

        /// <summary>Establishes the connection asynchronously.</summary>
        /// <param name="cancellationToken">Token to cancel the connect operation.</param>
        Task ConnectAsync(CancellationToken cancellationToken = default);
        /// <summary>Closes the connection asynchronously.</summary>
        /// <param name="cancellationToken">Token to cancel the disconnect operation.</param>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>Reads data from the connection into the provided buffer.</summary>
        /// <param name="buffer">The buffer to fill with received data.</param>
        /// <param name="cancellationToken">Token to cancel the read operation.</param>
        /// <returns>The number of bytes read.</returns>
        Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
        /// <summary>Writes data to the connection.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">Token to cancel the write operation.</param>
        Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

        /// <summary>Returns the underlying <see cref="Stream"/> for direct I/O operations.</summary>
        Stream GetStream();
    }
}
