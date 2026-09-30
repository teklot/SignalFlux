using System;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>Represents an IEEE-488.2 binary block payload exchanged over SCPI.</summary>
    public sealed class ScpiBlock
    {
        /// <summary>Creates a block wrapping the given payload bytes.</summary>
        /// <param name="data">The binary payload.</param>
        public ScpiBlock(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>The binary payload bytes.</summary>
        public byte[] Data { get; }

        /// <summary>The number of payload bytes.</summary>
        public int Length => Data.Length;

        /// <summary>Decodes the payload as a sequence of big-endian IEEE-754 double-precision values (the 8-byte waveform encoding used by many oscilloscopes).</summary>
        /// <returns>The decoded sample values.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the payload length is not a multiple of 8.</exception>
        public double[] ToDoubles()
        {
            if (Data.Length % 8 != 0)
                throw new InvalidOperationException("Block payload length must be a multiple of 8 to decode as IEEE-754 doubles.");
            var samples = new double[Data.Length / 8];
            for (int i = 0; i < samples.Length; i++)
            {
                ulong bits = 0;
                for (int b = 0; b < 8; b++)
                    bits = (bits << 8) | Data[i * 8 + b];
                samples[i] = BitConverter.Int64BitsToDouble((long)bits);
            }
            return samples;
        }
    }
}