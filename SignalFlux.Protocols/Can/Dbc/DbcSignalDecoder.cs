using System;
using System.Collections.Generic;
using SignalFlux;

namespace SignalFlux.Protocols.Can.Dbc
{
    /// <summary>
    /// Decodes raw <see cref="CanFrame"/> data into physical values using a <see cref="DbcMessage"/>
    /// signal layout. Handles Intel (little-endian) and Motorola (big-endian) signal layouts, signed values,
    /// factor/offset scaling, physical range validity, and message-level multiplexing.
    /// </summary>
    public sealed class DbcSignalDecoder
    {
        /// <summary>Creates a decoder for the given message definition.</summary>
        /// <param name="message">The DBC message definition that describes the frame layout.</param>
        public DbcSignalDecoder(DbcMessage message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>The message definition this decoder operates on.</summary>
        public DbcMessage Message { get; }

        /// <summary>
        /// Decodes a received frame into a set of physical values, keyed by signal name.
        /// Signals whose bits exceed the frame length, or that are gated off by multiplexing, are omitted.
        /// </summary>
        /// <param name="frame">The received CAN frame.</param>
        /// <returns>A dictionary mapping signal names to physical values.</returns>
        public IReadOnlyDictionary<string, double> Decode(CanFrame frame)
        {
            var result = new Dictionary<string, double>();
            foreach (var signal in Message.Signals.Values)
            {
                if (signal.IsMultiplexed)
                    continue;
                if (TryDecodeSignal(frame, signal, out double value))
                    result[signal.Name] = value;
            }
            return result;
        }

        /// <summary>
        /// Attempts to decode a single signal from a frame to its physical value.
        /// Returns false if the signal's bits lie outside the frame or the multiplexing condition is unmet.
        /// </summary>
        public bool TryDecodeSignal(CanFrame frame, DbcSignal signal, out double value)
        {
            value = double.NaN;
            if (signal == null) return false;
            if (!CoversBits(frame.Data.Length, signal.StartBit, signal.Length, signal.ByteOrder)) return false;

            if (signal.IsMultiplexed)
            {
                var multiplexor = Message.Multiplexor;
                if (multiplexor == null) return false;
                if (!TryDecodeSignal(frame, multiplexor, out double switchValue)) return false;
                if (signal.MultiplexerSwitchValue != (int)switchValue) return false;
            }

            double raw = signal.IsSigned
                ? frame.GetRawSignedValue(signal.StartBit, signal.Length, signal.ByteOrder)
                : (double)frame.GetRawValue(signal.StartBit, signal.Length, signal.ByteOrder);
            value = raw * signal.Factor + signal.Offset;
            return true;
        }

        /// <summary>
        /// Decodes a signal into a full <see cref="Measurement{T}"/> carrying the physical value, its quality
        /// (Bad when out of the signal's physical range), and contextual metadata including the DBC unit string.
        /// </summary>
        public Measurement<double> DecodeAsMeasurement(
            CanFrame frame, DbcSignal signal, string source = "dbc", Enum unit = null)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            if (!TryDecodeSignal(frame, signal, out double value))
                throw new InvalidOperationException(
                    $"Signal '{signal.Name}' could not be decoded from frame 0x{frame.Id:X3}.");

            Quality quality =
                (signal.Maximum > signal.Minimum && (value < signal.Minimum || value > signal.Maximum))
                    ? Quality.Bad
                    : Quality.Good;

            var metadata = new SignalFlux.Metadata()
                .With("source", source)
                .With("id", "0x" + frame.Id.ToString("X3"))
                .With("dbc_message", Message.Name)
                .With("dbc_signal", signal.Name);
            if (!string.IsNullOrEmpty(signal.Unit))
                metadata = metadata.With("dbc_unit", signal.Unit);

            return new Measurement<double>(value, frame.Timestamp, unit, quality, metadata);
        }

        private static bool CoversBits(int byteCount, int startBit, int length, CanByteOrder byteOrder) =>
            CanSignalExtensions.IsWithinPayload(byteCount, startBit, length, byteOrder);
    }
}
