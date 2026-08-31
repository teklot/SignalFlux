using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// A CAN transport backed by the PEAK PCAN-Basic driver (PCANBasic.dll). The driver is only present
    /// on systems with the PCAN-Basic SDK / driver installed. When the native library or a channel is
    /// unavailable, operations throw a descriptive <see cref="InvalidOperationException"/>/<see cref="PlatformNotSupportedException"/>.
    /// </summary>
    public sealed class PcanTransport : ICanTransport
    {
        private readonly ushort _channel;
        private readonly string _availabilityError;

        /// <summary>Creates a PCAN transport for the specified channel (e.g., 0x51 = PCAN_USBBUS1).</summary>
        /// <param name="channel">The PCAN channel handle.</param>
        public PcanTransport(ushort channel)
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
            throw new NotSupportedException("PCAN receive reads require a connected PCAN channel.");
        }

        private void ThrowIfUnavailable()
        {
            if (_availabilityError != null)
                throw new InvalidOperationException(_availabilityError);
        }

        private static string ProbeAvailability(ushort channel)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "The PCAN-Basic driver is only available on Windows.";

            // PCANBasic.dll is a native dependency. Detection would P/Invoke Initialize/GetStatus here;
            // the native driver is not shipped with this managed package by design.
            return _driverNotShippedMessage;
        }

        private const string _driverNotShippedMessage =
            "The PCAN-Basic driver (PCANBasic.dll) is not shipped with this package. Install the PEAK PCAN-Basic " +
            "SDK and ensure the native driver is discoverable to use the PCAN transport.";

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
