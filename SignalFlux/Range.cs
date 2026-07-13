using System;

namespace SignalFlux
{
    /// <summary>Represents a closed interval [Minimum, Maximum] for a comparable type.</summary>
    /// <typeparam name="T">The value type, which must implement <see cref="IComparable{T}"/>.</typeparam>
    public readonly struct Range<T> : IEquatable<Range<T>> where T : IComparable<T>
    {
        /// <summary>The inclusive lower bound of the range.</summary>
        public T Minimum { get; }
        /// <summary>The inclusive upper bound of the range.</summary>
        public T Maximum { get; }

        /// <summary>Creates a range with the specified bounds.</summary>
        /// <exception cref="ArgumentException">Thrown if <paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
        public Range(T minimum, T maximum)
        {
            if (minimum.CompareTo(maximum) > 0)
                throw new ArgumentException("Minimum must be less than or equal to maximum");
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>Returns true if <paramref name="value"/> lies within [Minimum, Maximum].</summary>
        public bool Contains(T value) =>
            value.CompareTo(Minimum) >= 0 && value.CompareTo(Maximum) <= 0;

        /// <summary>Returns true if this range is equal to another by comparing minimum and maximum.</summary>
        /// <param name="other">The other range to compare against.</param>
        public bool Equals(Range<T> other) =>
            Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);

        /// <summary>Returns true if this range is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Range<T> other && Equals(other);

        /// <summary>Returns a hash code for this range.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Minimum?.GetHashCode() ?? 0);
            hash = hash * 31 + (Maximum?.GetHashCode() ?? 0);
            return hash;
        }

        /// <summary>Returns true if two ranges are equal.</summary>
        public static bool operator ==(Range<T> left, Range<T> right) => left.Equals(right);
        /// <summary>Returns true if two ranges are not equal.</summary>
        public static bool operator !=(Range<T> left, Range<T> right) => !left.Equals(right);

        /// <summary>Returns a string representation of this range.</summary>
        public override string ToString() => $"[{Minimum}, {Maximum}]";
    }
}
