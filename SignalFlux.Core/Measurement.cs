using System;
using System.Collections.Generic;
using UnitsNet;

namespace SignalFlux
{
    /// <summary>A single timestamped data point with a value, unit, quality, and optional metadata.</summary>
    /// <typeparam name="T">The value type (e.g., double, float, int).</typeparam>
    public readonly struct Measurement<T> : IEquatable<Measurement<T>>
    {
        /// <summary>The measured value.</summary>
        public T Value { get; }
        /// <summary>The UTC time at which the measurement was taken.</summary>
        public Timestamp Timestamp { get; }
        /// <summary>The unit of measurement (e.g., <c>ElectricPotentialUnit.Volt</c>, <c>TemperatureUnit.DegreeCelsius</c>).</summary>
        public Enum Unit { get; }
        /// <summary>The quality or confidence level of this measurement.</summary>
        public Quality Quality { get; }
        /// <summary>Extended metadata attached to this measurement.</summary>
        public Metadata Metadata { get; }

        /// <summary>Creates a new measurement.</summary>
        /// <param name="value">The measured value.</param>
        /// <param name="timestamp">The UTC timestamp.</param>
        /// <param name="unit">The unit (e.g., <c>ElectricPotentialUnit.Volt</c>).</param>
        /// <param name="quality">The quality assessment.</param>
        /// <param name="metadata">Optional extended metadata.</param>
        public Measurement(
            T value,
            Timestamp timestamp,
            Enum unit = null,
            Quality quality = Quality.Good,
            Metadata metadata = null)
        {
            Value = value;
            Timestamp = timestamp;
            Unit = unit;
            Quality = quality;
            Metadata = metadata ?? new Metadata();
        }

        /// <summary>Returns a copy with the value replaced.</summary>
        public Measurement<T> WithValue(T value) =>
            new Measurement<T>(value, Timestamp, Unit, Quality, Metadata);

        /// <summary>Returns a copy with the timestamp replaced.</summary>
        public Measurement<T> WithTimestamp(Timestamp timestamp) =>
            new Measurement<T>(Value, timestamp, Unit, Quality, Metadata);

        /// <summary>Returns a copy with the unit replaced.</summary>
        public Measurement<T> WithUnit(Enum unit) =>
            new Measurement<T>(Value, Timestamp, unit, Quality, Metadata);

        /// <summary>Returns a copy with the quality replaced.</summary>
        public Measurement<T> WithQuality(Quality quality) =>
            new Measurement<T>(Value, Timestamp, Unit, quality, Metadata);

        /// <summary>Returns a copy with the metadata replaced.</summary>
        public Measurement<T> WithMetadata(Metadata metadata) =>
            new Measurement<T>(Value, Timestamp, Unit, Quality, metadata);

        /// <summary>Returns true if this measurement is equal to another by comparing value, timestamp, unit, and quality.</summary>
        /// <param name="other">The other measurement to compare against.</param>
        public bool Equals(Measurement<T> other) =>
            EqualityComparer<T>.Default.Equals(Value, other.Value) &&
            Timestamp.Equals(other.Timestamp) &&
            Equals(Unit, other.Unit) &&
            Quality == other.Quality;

        /// <summary>Returns true if this measurement is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Measurement<T> other && Equals(other);

        /// <summary>Returns a hash code for this measurement.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Value?.GetHashCode() ?? 0);
            hash = hash * 31 + Timestamp.GetHashCode();
            hash = hash * 31 + (Unit?.GetHashCode() ?? 0);
            hash = hash * 31 + (int)Quality;
            return hash;
        }

        /// <summary>Returns true if two measurements are equal.</summary>
        public static bool operator ==(Measurement<T> left, Measurement<T> right) => left.Equals(right);
        /// <summary>Returns true if two measurements are not equal.</summary>
        public static bool operator !=(Measurement<T> left, Measurement<T> right) => !left.Equals(right);

        /// <summary>Returns a string representation of this measurement.</summary>
        public override string ToString() =>
            $"{Value} {Unit} @ {Timestamp} [{Quality}]";
    }
}
