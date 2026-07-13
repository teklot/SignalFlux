using System;

namespace SignalFlux
{
    /// <summary>Defines a time window with a start and duration.</summary>
    public readonly struct Window : IEquatable<Window>
    {
        /// <summary>The start of the window.</summary>
        public Timestamp Start { get; }
        /// <summary>The duration of the window (must be positive).</summary>
        public TimeSpan Duration { get; }

        /// <summary>Creates a window from a start time and duration.</summary>
        /// <exception cref="ArgumentException">Thrown if <paramref name="duration"/> is zero or negative.</exception>
        public Window(Timestamp start, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                throw new ArgumentException("Duration must be positive", nameof(duration));
            Start = start;
            Duration = duration;
        }

        /// <summary>The end time of the window (Start + Duration).</summary>
        public Timestamp End => Start + Duration;

        /// <summary>Creates a window from start and end timestamps.</summary>
        public static Window FromStartEnd(Timestamp start, Timestamp end) =>
            new Window(start, end - start);

        /// <summary>Returns true if the timestamp falls within [Start, End).</summary>
        public bool Contains(Timestamp timestamp) =>
            timestamp >= Start && timestamp < End;

        /// <summary>Returns true if this window overlaps with another.</summary>
        public bool Overlaps(Window other) =>
            Start < other.End && other.Start < End;

        /// <summary>Returns true if this window is equal to another by comparing start and duration.</summary>
        public bool Equals(Window other) =>
            Start.Equals(other.Start) && Duration.Equals(other.Duration);

        /// <summary>Returns true if this window is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Window other && Equals(other);

        /// <summary>Returns a hash code for this window.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Start.GetHashCode();
            hash = hash * 31 + Duration.GetHashCode();
            return hash;
        }

        /// <summary>Returns true if two windows are equal.</summary>
        public static bool operator ==(Window left, Window right) => left.Equals(right);
        /// <summary>Returns true if two windows are not equal.</summary>
        public static bool operator !=(Window left, Window right) => !left.Equals(right);

        /// <summary>Returns a string representation of this window.</summary>
        public override string ToString() => $"[{Start} + {Duration}]";
    }
}
