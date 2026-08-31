using System;

namespace SignalFlux.Protocols.Can
{
    /// <summary>Byte ordering used when encoding/decoding multi-byte CAN signals.</summary>
    public enum CanByteOrder
    {
        /// <summary>Least-significant byte first (little-endian, DBC byte_order=0 / Intel).</summary>
        LittleEndian = 0,

        /// <summary>Most-significant byte first (big-endian, DBC byte_order=1 / Motorola).</summary>
        BigEndian = 1,
    }

    /// <summary>
    /// Extension methods for converting CAN frame payloads to and from raw and physical signal values.
    /// Supports the standard DBC bit-layout conventions (start bit, signal length, Intel/Motorola byte
    /// order, and factor/offset scaling).
    /// </summary>
    public static class CanSignalExtensions
    {
        // ------------------------------------------------------------------
        // Raw integer extraction from a frame payload
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads an unsigned integer signal from the frame payload using the DBC start-bit / length
        /// convention. For Intel (little-endian) signals the start bit is the least-significant bit.
        /// For Motorola (big-endian) signals the start bit refers to the most significant byte position.
        /// </summary>
        /// <param name="frame">The frame to read from.</param>
        /// <param name="startBit">The DBC start bit (0-based bit position within the 64-bit payload).</param>
        /// <param name="length">The signal width in bits (1–64).</param>
        /// <param name="byteOrder">The byte ordering convention.</param>
        /// <returns>The raw (unscaled) unsigned signal value.</returns>
        public static ulong GetRawValue(this CanFrame frame, int startBit, int length, CanByteOrder byteOrder)
        {
            return GetRawValue(frame.Data.Span, startBit, length, byteOrder);
        }

        /// <summary>
        /// Reads an unsigned integer signal from a raw payload span using the DBC start-bit / length convention.
        /// </summary>
        public static ulong GetRawValue(ReadOnlySpan<byte> payload, int startBit, int length, CanByteOrder byteOrder)
        {
            if (startBit < 0) throw new ArgumentOutOfRangeException(nameof(startBit));
            if (length < 1 || length > 64) throw new ArgumentOutOfRangeException(nameof(length), "Signal length must be 1..64 bits.");
            ValidateSpan(payload, startBit, length, byteOrder);

            ulong value = 0;
            for (int bit = 0; bit < length; bit++)
            {
                if (byteOrder == CanByteOrder.LittleEndian)
                {
                    if (IsBitSet(payload, startBit + bit))
                        value |= 1UL << bit;
                }
                else
                {
                    // Motorola: walk from the MSB (start bit) toward the LSB, wrapping from a
                    // byte's LSB up to the next byte's MSB (the classic big-endian "sawtooth").
                    int cursor = startBit;
                    for (int i = length - 1; i >= 0; i--)
                    {
                        if (IsBitSet(payload, cursor))
                            value |= 1UL << i;
                        cursor = cursor % 8 == 0 ? cursor + 15 : cursor - 1;
                    }
                }
            }
            return value;
        }

        /// <summary>Reads a signed integer signal from the frame payload.</summary>
        public static long GetRawSignedValue(this CanFrame frame, int startBit, int length, CanByteOrder byteOrder)
        {
            ulong raw = GetRawValue(frame, startBit, length, byteOrder);
            return SignExtend(raw, length);
        }

        // ------------------------------------------------------------------
        // Writing raw integers into a frame payload
        // ------------------------------------------------------------------

        /// <summary>Encodes an unsigned signal value into a new frame payload using the DBC convention.</summary>
        /// <param name="rawValue">The raw (unscaled) value to encode.</param>
        /// <param name="startBit">The DBC start bit.</param>
        /// <param name="length">The signal width in bits (1–64).</param>
        /// <param name="byteOrder">The byte ordering convention.</param>
        /// <returns>An 8-byte payload with the signal laid out.</returns>
        public static byte[] EncodeRawValue(ulong rawValue, int startBit, int length, CanByteOrder byteOrder)
        {
            if (length < 1 || length > 64) throw new ArgumentOutOfRangeException(nameof(length));
            var payload = new byte[CanFrame.MaxPayloadLength];
            if (byteOrder == CanByteOrder.LittleEndian)
            {
                for (int bit = 0; bit < length; bit++)
                {
                    if (((rawValue >> bit) & 1UL) == 1UL)
                    {
                        int byteIndex = (startBit + bit) / 8;
                        int bitInByte = (startBit + bit) % 8;
                        payload[byteIndex] |= (byte)(1 << bitInByte);
                    }
                }
            }
            else
            {
                int cursor = startBit;
                for (int i = length - 1; i >= 0; i--)
                {
                    if (((rawValue >> i) & 1UL) == 1UL)
                    {
                        int byteIndex = cursor / 8;
                        int bitInByte = cursor % 8;
                        payload[byteIndex] |= (byte)(1 << bitInByte);
                    }
                    cursor = cursor % 8 == 0 ? cursor + 15 : cursor - 1;
                }
            }
            return payload;
        }

        // ------------------------------------------------------------------
        // Physical (scaled) conversion to / from Signal & Measurement
        // ------------------------------------------------------------------

        /// <summary>Decodes a physical double value with factor/offset scaling from a frame's raw signal.</summary>
        /// <param name="frame">The frame to decode from.</param>
        /// <param name="startBit">The DBC start bit.</param>
        /// <param name="length">The signal width in bits.</param>
        /// <param name="factor">The scaling factor multiplied by the raw value.</param>
        /// <param name="offset">The offset added to the scaled value.</param>
        /// <param name="signed">Whether the signal is signed (two's complement).</param>
        /// <param name="byteOrder">The byte ordering convention.</param>
        /// <returns>The physical value.</returns>
        public static double ToPhysicalValue(
            this CanFrame frame,
            int startBit,
            int length,
            double factor,
            double offset,
            bool signed = false,
            CanByteOrder byteOrder = CanByteOrder.LittleEndian)
        {
            if (signed)
                return GetRawSignedValue(frame, startBit, length, byteOrder) * factor + offset;
            return GetRawValue(frame, startBit, length, byteOrder) * factor + offset;
        }

        /// <summary>
        /// Encodes a physical double value into an 8-byte payload using factor/offset scaling and the
        /// DBC bit-layout convention.
        /// </summary>
        public static byte[] EncodePhysicalValue(
            double value,
            int startBit,
            int length,
            double factor,
            double offset,
            bool signed = false,
            CanByteOrder byteOrder = CanByteOrder.LittleEndian)
        {
            double raw = (value - offset) / factor;
            raw = Math.Round(raw, 0, MidpointRounding.AwayFromZero);

            ulong bits;
            if (signed)
            {
                long signedRaw = (long)raw;
                bits = unchecked((ulong)signedRaw) & (length < 64 ? ((1UL << length) - 1) : ulong.MaxValue);
            }
            else
            {
                bits = raw < 0 ? 0 : (ulong)raw;
            }
            return EncodeRawValue(bits, startBit, length, byteOrder);
        }

        /// <summary>Converts a CAN frame signal into a <see cref="Measurement{T}"/>.</summary>
        public static Measurement<double> ToMeasurement(
            this CanFrame frame,
            int startBit = 0,
            int length = 32,
            double factor = 1.0,
            double offset = 0.0,
            bool signed = false,
            CanByteOrder byteOrder = CanByteOrder.LittleEndian,
            Enum unit = null,
            float physicalMinimum = 0f,
            float physicalMaximum = 0f,
            string source = "can")
        {
            double value = ToPhysicalValue(frame, startBit, length, factor, offset, signed, byteOrder);
            Quality quality = (physicalMaximum > physicalMinimum &&
                               (value < physicalMinimum || value > physicalMaximum))
                ? Quality.Bad
                : Quality.Good;

            var metadata = new Metadata()
                .With("id", ((int)frame.Id).ToString("X3"))
                .With("bus", "can")
                .With("dlc", frame.Data.Length.ToString())
                .With("source", source);

            return new Measurement<double>(value, frame.Timestamp, unit, quality, metadata);
        }

        // ------------------------------------------------------------------
        // Bit helpers
        // ------------------------------------------------------------------

        private static bool IsBitSet(ReadOnlySpan<byte> payload, int bitIndex)
        {
            int byteIndex = bitIndex / 8;
            int bitInByte = bitIndex % 8;
            return (payload[byteIndex] & (1 << bitInByte)) != 0;
        }

        private static long SignExtend(ulong raw, int length)
        {
            if (length == 64)
                return unchecked((long)raw);
            ulong signBit = 1UL << (length - 1);
            if ((raw & signBit) != 0)
                return unchecked((long)(raw | (~((1UL << length) - 1))));
            return (long)raw;
        }

        private static void ValidateSpan(
            ReadOnlySpan<byte> payload, int startBit, int length, CanByteOrder byteOrder)
        {
            if (!IsWithinPayload(payload.Length, startBit, length, byteOrder))
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Signal extends past the {payload.Length}-byte payload.");
        }

        /// <summary>Returns whether a signal of the given layout fits within a byte-count payload.</summary>
        public static bool IsWithinPayload(int byteCount, int startBit, int length, CanByteOrder byteOrder)
        {
            if (startBit < 0 || length < 1 || length > 64 || byteCount < 0) return false;
            int maxBit = byteOrder == CanByteOrder.BigEndian
                ? MotorolaMaxBit(startBit, length)
                : startBit + length - 1;
            return maxBit < byteCount * 8;
        }

        // For a Motorola (big-endian) signal the start bit names the MSB and the signal walks the
        // classic "sawtooth": down through the start byte to its LSB, then up to the next byte's MSB.
        // Returns the highest linear bit position the signal can occupy.
        private static int MotorolaMaxBit(int startBit, int length)
        {
            int bitsInStartByte = startBit % 8 + 1;
            int remaining = length - bitsInStartByte;
            if (remaining <= 0)
                return startBit / 8 * 8 + 7;
            return (startBit / 8 + (remaining + 7) / 8) * 8 + 7;
        }
    }
}
