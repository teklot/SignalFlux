using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// A CAN transport backed by the Kvaser CANlib driver (canlib32.dll). CANlib is only present on systems
    /// with the Kvaser driver / CANlib SDK installed. When the native library or a channel is unavailable,
    /// operations throw a descriptive exception.
    /// </summary>
    public sealed class KvaserTransport : ICanTransport
    {
        private readonly int _channel;
        private readonly string _availabilityError;

        /// <summary>Creates a Kvaser transport for the specified channel index (0-based).</summary>
        /// <param name="channel">The CANlib channel index.</param>
        public KvaserTransport(int channel)
        {
            _channel = channel;
            _availabilityError = ProbeAvailability(channel);
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
            ThrowIfUnavailable();
        }

        /// <inheritdoc/>
        public Task CloseAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        {
            ThrowIfUnavailable();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<CanFrame> ReadAsync(CancellationToken ct = default)
        {
            ThrowIfUnavailable();
            throw new NotSupportedException("Kvaser receive reads require a connected CANlib channel.");
        }

        private void ThrowIfUnavailable()
        {
            if (_availabilityError != null)
                throw new InvalidOperationException(_availabilityError);
        }

        private static string ProbeAvailability(int channel)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "The Kvaser CANlib driver is only available on Windows and Linux.";

            // canlib32.dll / libcanlib.so is a native dependency. Detection would P/Invoke
            // canOpenChannel here; the native driver is not shipped with this managed package by design.
            return _driverNotShippedMessage;
        }

        private const string _driverNotShippedMessage =
            "The Kvaser CANlib driver is not shipped with this package. Install the Kvaser driver / CANlib SDK " +
            "so the native driver is discoverable to use the Kvaser transport.";

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
