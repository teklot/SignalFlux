using System;
using SignalFlux;

namespace SignalFlux.Space.Ccsds
{
    /// <summary>
    /// A CCSDS Day-Segmented Time Code (CDS), CCSDS 301.0-B: a 16-bit day count plus milliseconds of
    /// day elapsed since a mission-defined epoch, with an optional 16-bit sub-millisecond fraction
    /// (in units of 2^-16 of a millisecond).
    /// </summary>
    public readonly struct CdsTime
    {
        /// <summary>The epoch from which the day count is counted (defined by the sender per the P-field).</summary>
        public Timestamp Epoch { get; }

        /// <summary>The number of whole days elapsed since the epoch.</summary>
        public ushort Days { get; }

        /// <summary>The number of milliseconds elapsed in the current day (0-86_399_999).</summary>
        public uint MillisOfDay { get; }

        /// <summary>The sub-millisecond fraction in units of 2^-16 of a millisecond (0 when absent).</summary>
        public ushort SubMilli { get; }

        /// <summary>Creates a CDS time from its logical components.</summary>
        /// <param name="epoch">The mission-defined epoch.</param>
        /// <param name="days">The whole-day count.</param>
        /// <param name="millisOfDay">The milliseconds of the current day.</param>
        /// <param name="subMilli">The sub-millisecond fraction in 2^-16 millisecond units.</param>
        public CdsTime(Timestamp epoch, ushort days, uint millisOfDay, ushort subMilli = 0)
        {
            if (millisOfDay >= 86_400_000)
                throw new ArgumentOutOfRangeException(nameof(millisOfDay), "Milliseconds of day must be below 86,400,000.");

            Epoch = epoch;
            Days = days;
            MillisOfDay = millisOfDay;
            SubMilli = subMilli;
        }

        /// <summary>
        /// Decodes a CDS time-code field. Supported layouts are `[days:2][millisOfDay:3]` (5 octets)
        /// and `[days:2][millisOfDay:3][subMilli:2]` (7 octets), big-endian.
        /// </summary>
        /// <param name="timeField">The CDS time-code field (without the P-field).</param>
        /// <param name="epoch">The mission-defined epoch.</param>
        /// <exception cref="ArgumentException">Thrown when the field width is not a supported CDS layout.</exception>
        public static CdsTime Decode(ReadOnlySpan<byte> timeField, Timestamp epoch)
        {
            if (timeField.Length != 5 && timeField.Length != 7)
                throw new ArgumentException(
                    "CDS supports 5-octet (days + millisOfDay) or 7-octet (days + millisOfDay + subMilli) fields.",
                    nameof(timeField));

            ushort days = (ushort)((timeField[0] << 8) | timeField[1]);
            uint millis = ((uint)timeField[2] << 16) | ((uint)timeField[3] << 8) | timeField[4];
            ushort subMilli = timeField.Length == 7
                ? (ushort)((timeField[5] << 8) | timeField[6])
                : (ushort)0;

            return new CdsTime(epoch, days, millis, subMilli);
        }

        /// <summary>Converts this time code to an absolute <see cref="Timestamp"/>.</summary>
        public Timestamp ToTimestamp()
        {
            double totalMilliseconds = MillisOfDay + SubMilli / 65536.0;
            return Epoch + TimeSpan.FromDays(Days) + TimeSpan.FromMilliseconds(totalMilliseconds);
        }

        /// <summary>Returns the time formatted as days and milliseconds since the epoch.</summary>
        public override string ToString() =>
            $"{Days}d {MillisOfDay}ms since {Epoch.DateTime:O}";
    }
}