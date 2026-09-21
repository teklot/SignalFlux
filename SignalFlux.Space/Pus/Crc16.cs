using System;

namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// The CRC-16-CCITT cyclic redundancy check used by ECSS PUS TM packets: polynomial 0x1021,
    /// initial value 0xFFFF.
    /// </summary>
    public static class Crc16
    {
        /// <summary>Computes the PUS CRC-16 over the given octets.</summary>
        /// <param name="data">The octets to checksum (typically the complete packet).</param>
        /// <returns>The 16-bit checksum.</returns>
        public static ushort Compute(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ 0x1021)
                        : (ushort)(crc << 1);
            }
            return crc;
        }

        /// <summary>Returns true when the computed CRC over <paramref name="data"/> matches the expected value.</summary>
        public static bool Verify(ReadOnlySpan<byte> data, ushort expected) =>
            Compute(data) == expected;
    }
}