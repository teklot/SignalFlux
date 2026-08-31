using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// Abstraction over a CAN bus transport. Implementations wrap platform-specific drivers
    /// (SocketCAN on Linux, PCAN-Basic, Kvaser CANlib, or an in-memory loopback for testing).
    /// </summary>
    public interface ICanTransport : IAsyncDisposable
    {
        /// <summary>Raised when a frame is received on the bus.</summary>
        event EventHandler<CanFrameReceivedEventArgs> FrameReceived;

        /// <summary>Opens the transport and begins receiving frames from the bus.</summary>
        /// <param name="ct">Cancellation token for the open operation.</param>
        Task OpenAsync(CancellationToken ct = default);

        /// <summary>Closes the transport and stops receiving frames.</summary>
        Task CloseAsync();

        /// <summary>Transmits a single frame on the bus.</summary>
        /// <param name="frame">The frame to send.</param>
        /// <param name="ct">Cancellation token for the send operation.</param>
        Task SendAsync(CanFrame frame, CancellationToken ct = default);

        /// <summary>Reads a single frame from the bus, waiting until one is available.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The next received frame.</returns>
        Task<CanFrame> ReadAsync(CancellationToken ct = default);
    }

    /// <summary>Provides data for the <see cref="ICanTransport.FrameReceived"/> event.</summary>
    public sealed class CanFrameReceivedEventArgs : EventArgs
    {
        /// <summary>The received frame.</summary>
        public CanFrame Frame { get; }

        /// <summary>Creates event args carrying a received frame.</summary>
        public CanFrameReceivedEventArgs(CanFrame frame)
        {
            Frame = frame;
        }
    }
}
