using System;

namespace SignalFlux
{
    /// <summary>Represents a precise moment in time as a UTC tick count, wrapping <see cref="DateTime"/>.</summary>
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        /// <summary>The underlying tick count (100-nanosecond intervals since 0001-01-01 00:00:00 UTC).</summary>
        public long Ticks { get; }

        /// <summary>The UTC DateTime representation of this timestamp.</summary>
        public DateTime DateTime => new DateTime(Ticks, DateTimeKind.Utc);

        /// <summary>Creates a timestamp from a tick count.</summary>
        public Timestamp(long ticks)
        {
            Ticks = ticks;
        }

        /// <summary>Returns a timestamp representing the current UTC time.</summary>
        public static Timestamp Now => new Timestamp(DateTime.UtcNow.Ticks);

        /// <summary>Returns a timestamp representing the current UTC time.</summary>
        public static Timestamp UtcNow => Now;

        /// <summary>The minimum representable timestamp (0001-01-01).</summary>
        public static Timestamp MinValue => new Timestamp(DateTime.MinValue.Ticks);

        /// <summary>The maximum representable timestamp (9999-12-31).</summary>
        public static Timestamp MaxValue => new Timestamp(DateTime.MaxValue.Ticks);

        /// <summary>Returns a timestamp at the epoch (0001-01-01).</summary>
        public static Timestamp Zero => new Timestamp(0);

        /// <summary>Creates a timestamp from a <see cref="DateTime"/> (kind is treated as UTC).</summary>
        public static Timestamp FromDateTime(DateTime dateTime) =>
            new Timestamp(dateTime.Ticks);

        /// <summary>Creates a timestamp from Unix milliseconds since 1970-01-01.</summary>
        public static Timestamp FromUnixMilliseconds(long ms) =>
            new Timestamp(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcTicks);

        /// <summary>Creates a timestamp from Unix seconds since 1970-01-01.</summary>
        public static Timestamp FromUnixSeconds(long seconds) =>
            new Timestamp(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcTicks);

        /// <summary>Converts this timestamp to Unix milliseconds since 1970-01-01.</summary>
        public long ToUnixMilliseconds() =>
            new DateTimeOffset(DateTime).ToUnixTimeMilliseconds();

        /// <summary>Converts this timestamp to Unix seconds since 1970-01-01.</summary>
        public long ToUnixSeconds() =>
            new DateTimeOffset(DateTime).ToUnixTimeSeconds();

        /// <summary>Returns the time elapsed since <paramref name="other"/>.</summary>
        public TimeSpan TimeSince(Timestamp other) =>
            TimeSpan.FromTicks(Ticks - other.Ticks);

        /// <summary>Computes the difference between two timestamps.</summary>
        public static TimeSpan operator -(Timestamp a, Timestamp b) =>
            TimeSpan.FromTicks(a.Ticks - b.Ticks);

        /// <summary>Adds a duration to a timestamp.</summary>
        public static Timestamp operator +(Timestamp t, TimeSpan duration) =>
            new Timestamp(t.Ticks + duration.Ticks);

        /// <summary>Subtracts a duration from a timestamp.</summary>
        public static Timestamp operator -(Timestamp t, TimeSpan duration) =>
            new Timestamp(t.Ticks - duration.Ticks);

        /// <summary>Returns true if this timestamp is equal to another by comparing ticks.</summary>
        public bool Equals(Timestamp other) => Ticks == other.Ticks;

        /// <summary>Returns true if this timestamp is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Timestamp other && Equals(other);

        /// <summary>Returns a hash code for this timestamp.</summary>
        public override int GetHashCode() => Ticks.GetHashCode();

        /// <summary>Compares this timestamp to another by tick count.</summary>
        public int CompareTo(Timestamp other) => Ticks.CompareTo(other.Ticks);

        /// <summary>Returns true if two timestamps are equal.</summary>
        public static bool operator ==(Timestamp left, Timestamp right) => left.Equals(right);
        /// <summary>Returns true if two timestamps are not equal.</summary>
        public static bool operator !=(Timestamp left, Timestamp right) => !left.Equals(right);
        /// <summary>Returns true if left is earlier than right.</summary>
        public static bool operator <(Timestamp left, Timestamp right) => left.Ticks < right.Ticks;
        /// <summary>Returns true if left is later than right.</summary>
        public static bool operator >(Timestamp left, Timestamp right) => left.Ticks > right.Ticks;
        /// <summary>Returns true if left is earlier than or equal to right.</summary>
        public static bool operator <=(Timestamp left, Timestamp right) => left.Ticks <= right.Ticks;
        /// <summary>Returns true if left is later than or equal to right.</summary>
        public static bool operator >=(Timestamp left, Timestamp right) => left.Ticks >= right.Ticks;

        /// <summary>Returns an ISO 8601 string representation of this timestamp.</summary>
        public override string ToString() => DateTime.ToString("O");
    }
}
