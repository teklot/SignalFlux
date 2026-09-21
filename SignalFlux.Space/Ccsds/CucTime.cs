using System;
using SignalFlux;

namespace SignalFlux.Space.Ccsds
{
    /// <summary>
    /// A CCSDS Unsegmented Time Code (CUC), CCSDS 301.0-B: a coarse/fine binary counter of seconds
    /// elapsed since a mission-defined epoch. The fine field is in units of 2^-n seconds, where n is the
    /// number of sub-second bits (equal to fractionalBytes x 8).
    /// </summary>
    public readonly struct CucTime
    {
        /// <summary>The epoch from which time is counted (defined by the sender per the P-field).</summary>
        public Timestamp Epoch { get; }

        /// <summary>The coarse (whole-second) component.</summary>
        public ulong Coarse { get; }

        /// <summary>The fine (fractional-second) component, in units of 2^-<see cref="SubSecondBits"/> seconds.</summary>
        public uint Fine { get; }

        /// <summary>The number of sub-second bits carried by the fine field.</summary>
        public int SubSecondBits { get; }

        /// <summary>Creates a CUC time from its logical components.</summary>
        /// <param name="epoch">The mission-defined epoch.</param>
        /// <param name="coarse">The whole-second component.</param>
        /// <param name="fine">The fractional component in units of 2^-n seconds.</param>
        /// <param name="subSecondBits">The number of sub-second bits (1-32).</param>
        public CucTime(Timestamp epoch, ulong coarse, uint fine, int subSecondBits)
        {
            if (subSecondBits < 1 || subSecondBits > 32)
                throw new ArgumentOutOfRangeException(nameof(subSecondBits));
            if (fine >= (1u << subSecondBits))
                throw new ArgumentOutOfRangeException(nameof(fine), "Fine component exceeds the sub-second field width.");

            Epoch = epoch;
            Coarse = coarse;
            Fine = fine;
            SubSecondBits = subSecondBits;
        }

        /// <summary>
        /// Decodes a CUC time field of coarseBytes whole-second octets followed by fractionalBytes
        /// sub-second octets.
        /// </summary>
        /// <param name="timeField">The CUC time-code field (without the P-field).</param>
        /// <param name="epoch">The mission-defined epoch.</param>
        /// <param name="coarseBytes">Number of octets in the coarse (whole-second) component (1-8).</param>
        /// <param name="fractionalBytes">Number of octets in the fractional component (1-4).</param>
        /// <exception cref="ArgumentException">Thrown when the field width does not match a <see cref="CucTime"/>.</exception>
        public static CucTime Decode(ReadOnlySpan<byte> timeField, Timestamp epoch, int coarseBytes = 4, int fractionalBytes = 2)
        {
            if (coarseBytes < 1 || coarseBytes > 8)
                throw new ArgumentOutOfRangeException(nameof(coarseBytes));
            if (fractionalBytes < 1 || fractionalBytes > 4)
                throw new ArgumentOutOfRangeException(nameof(fractionalBytes));
            if (timeField.Length != coarseBytes + fractionalBytes)
                throw new ArgumentException(
                    $"A CUC field of {coarseBytes}/{fractionalBytes} octets requires exactly {coarseBytes + fractionalBytes} octets.",
                    nameof(timeField));

            ulong coarse = 0;
            for (int i = 0; i < coarseBytes; i++)
                coarse = (coarse << 8) | timeField[i];

            uint fine = 0;
            for (int i = 0; i < fractionalBytes; i++)
                fine = (fine << 8) | timeField[coarseBytes + i];

            return new CucTime(epoch, coarse, fine, fractionalBytes * 8);
        }

        /// <summary>Converts this time code to an absolute <see cref="Timestamp"/>.</summary>
        public Timestamp ToTimestamp()
        {
            double seconds = Coarse + (double)Fine / (double)(1UL << SubSecondBits);
            return Epoch + TimeSpan.FromSeconds(seconds);
        }

        /// <summary>Returns the time formatted as seconds since the epoch.</summary>
        public override string ToString() =>
            string.Format("{0}.{1} s since {2:O}", Coarse, Fine.ToString("D" + (SubSecondBits / 4)), Epoch.DateTime);
    }
}