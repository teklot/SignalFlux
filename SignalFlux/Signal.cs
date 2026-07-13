using System;
using System.Collections.Generic;
using UnitsNet;

namespace SignalFlux
{
    /// <summary>Represents a uniformly-sampled time-domain signal with metadata.</summary>
    /// <typeparam name="T">The sample type (e.g., double, float, int).</typeparam>
    public readonly struct Signal<T> : IEquatable<Signal<T>>
    {
        /// <summary>The underlying sample data as read-only memory.</summary>
        public ReadOnlyMemory<T> Samples { get; }
        /// <summary>The sampling frequency in Hz (samples per second).</summary>
        public double Frequency { get; }
        /// <summary>The UTC time of the first sample.</summary>
        public Timestamp StartTime { get; }
        /// <summary>The unit of measurement (e.g., <c>ElectricPotentialUnit.Volt</c>, <c>TemperatureUnit.DegreeCelsius</c>).</summary>
        public Enum Unit { get; }
        /// <summary>Arbitrary tags for categorisation or filtering.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }
        /// <summary>An identifier for the data source or acquisition system.</summary>
        public string Source { get; }
        /// <summary>Extended metadata attached to the signal.</summary>
        public Metadata Metadata { get; }
        /// <summary>The overall quality assessment of this signal.</summary>
        public Quality Quality { get; }

        /// <summary>Creates a new signal with the specified parameters.</summary>
        /// <param name="samples">The sample data.</param>
        /// <param name="frequency">Sampling frequency in Hz (must be positive).</param>
        /// <param name="startTime">UTC time of the first sample.</param>
        /// <param name="unit">Unit of measurement (e.g., <c>ElectricPotentialUnit.Volt</c>).</param>
        /// <param name="tags">Optional categorisation tags.</param>
        /// <param name="source">Optional source identifier.</param>
        /// <param name="metadata">Optional extended metadata.</param>
        /// <param name="quality">Quality assessment.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="frequency"/> is zero or negative.</exception>
        public Signal(
            ReadOnlyMemory<T> samples,
            double frequency,
            Timestamp startTime,
            Enum unit = null,
            IReadOnlyDictionary<string, string> tags = null,
            string source = null,
            Metadata metadata = null,
            Quality quality = Quality.Good)
        {
            if (frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive");
            Samples = samples;
            Frequency = frequency;
            StartTime = startTime;
            Unit = unit;
            Tags = tags ?? new Dictionary<string, string>();
            Source = source ?? string.Empty;
            Metadata = metadata ?? new Metadata();
            Quality = quality;
        }

        /// <summary>The number of samples in the signal.</summary>
        public int Count => Samples.Length;

        /// <summary>The total duration of the signal (Count / Frequency).</summary>
        public TimeSpan Duration => TimeSpan.FromSeconds(Count / Frequency);

        /// <summary>The time between consecutive samples (1 / Frequency).</summary>
        public TimeSpan SampleInterval => TimeSpan.FromSeconds(1.0 / Frequency);

        /// <summary>The UTC time of the last sample (StartTime + Duration).</summary>
        public Timestamp EndTime => StartTime + Duration;

        /// <summary>Returns a copy with the samples replaced.</summary>
        public Signal<T> WithSamples(ReadOnlyMemory<T> samples) =>
            new Signal<T>(samples, Frequency, StartTime, Unit, Tags, Source, Metadata, Quality);

        /// <summary>Returns a copy with the frequency replaced.</summary>
        public Signal<T> WithFrequency(double frequency) =>
            new Signal<T>(Samples, frequency, StartTime, Unit, Tags, Source, Metadata, Quality);

        /// <summary>Returns a copy with the start time replaced.</summary>
        public Signal<T> WithStartTime(Timestamp startTime) =>
            new Signal<T>(Samples, Frequency, startTime, Unit, Tags, Source, Metadata, Quality);

        /// <summary>Returns a copy with the unit replaced.</summary>
        public Signal<T> WithUnit(Enum unit) =>
            new Signal<T>(Samples, Frequency, StartTime, unit, Tags, Source, Metadata, Quality);

        /// <summary>Returns a copy with the metadata replaced.</summary>
        public Signal<T> WithMetadata(Metadata metadata) =>
            new Signal<T>(Samples, Frequency, StartTime, Unit, Tags, Source, metadata, Quality);

        /// <summary>Returns a copy with the quality replaced.</summary>
        public Signal<T> WithQuality(Quality quality) =>
            new Signal<T>(Samples, Frequency, StartTime, Unit, Tags, Source, Metadata, quality);

        /// <summary>Returns true if this signal is equal to another by comparing samples, frequency, start time, unit, and quality.</summary>
        /// <param name="other">The other signal to compare against.</param>
        public bool Equals(Signal<T> other)
        {
            if (!Frequency.Equals(other.Frequency) ||
                !StartTime.Equals(other.StartTime) ||
                !Equals(Unit, other.Unit) ||
                Quality != other.Quality)
                return false;

            var span = Samples.Span;
            var otherSpan = other.Samples.Span;
            if (span.Length != otherSpan.Length)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                    return false;
            }
            return true;
        }

        /// <summary>Returns true if this signal is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Signal<T> other && Equals(other);

        /// <summary>Returns a hash code for this signal.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Frequency.GetHashCode();
            hash = hash * 31 + StartTime.GetHashCode();
            hash = hash * 31 + (Unit?.GetHashCode() ?? 0);
            hash = hash * 31 + (int)Quality;
            return hash;
        }

        /// <summary>Returns true if two signals are equal.</summary>
        public static bool operator ==(Signal<T> left, Signal<T> right) => left.Equals(right);
        /// <summary>Returns true if two signals are not equal.</summary>
        public static bool operator !=(Signal<T> left, Signal<T> right) => !left.Equals(right);
    }
}
