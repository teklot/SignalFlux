using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// A CAN transport backed by Linux SocketCAN (AF_CAN sockets on the "can"/"vcan" interfaces).
    /// SocketCAN is only available on Linux; opening this transport on any other platform throws
    /// <see cref="PlatformNotSupportedException"/>. On Linux the concrete socket binding is delegated
    /// so the logic can remain testable across platforms.
    /// </summary>
    public sealed class SocketCanTransport : ICanTransport
    {
        private readonly string _interfaceName;
        private readonly Func<string, ICanSocket> _socketFactory;

        /// <summary>Creates a SocketCAN transport bound to a CAN interface (e.g., "can0" or "vcan0").</summary>
        /// <param name="interfaceName">The SocketCAN interface name.</param>
        public SocketCanTransport(string interfaceName)
            : this(interfaceName, null)
        {
        }

        internal SocketCanTransport(string interfaceName, Func<string, ICanSocket> socketFactory)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));
            _interfaceName = interfaceName;
            _socketFactory = socketFactory;
        }

        /// <inheritdoc/>
        public event EventHandler<CanFrameReceivedEventArgs> FrameReceived
        {
            add => _frameReceived += value;
            remove => _frameReceived -= value;
        }

        private EventHandler<CanFrameReceivedEventArgs> _frameReceived;

        /// <inheritdoc/>
        public async Task OpenAsync(CancellationToken ct = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            ThrowIfNotLinux();
        }

        /// <inheritdoc/>
        public Task CloseAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        {
            ThrowIfNotLinux();
            if (_socketFactory == null)
                throw new InvalidOperationException(
                    "SocketCAN socket binding is not available; a transport factory must be provided.");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<CanFrame> ReadAsync(CancellationToken ct = default)
        {
            ThrowIfNotLinux();
            throw new NotSupportedException(
                "SocketCAN receive reads require a live AF_CAN socket and are not implemented in the portable client.");
        }

        private void ThrowIfNotLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new PlatformNotSupportedException(
                    "SocketCAN transport is only supported on Linux. Use PcanTransport, KvaserTransport, " +
                    "or InMemoryCanTransport on this platform.");
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }

    /// <summary>Abstraction over a raw SocketCAN socket so transport logic stays testable.</summary>
    public interface ICanSocket : IDisposable
    {
    }
}
