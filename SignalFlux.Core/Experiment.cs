using System;
using System.Collections.Generic;

namespace SignalFlux
{
    /// <summary>Represents a single experimental run, grouping signals, events, configuration, and equipment.</summary>
    public sealed class Experiment : IEquatable<Experiment>
    {
        /// <summary>A unique identifier for the experiment.</summary>
        public string Id { get; }
        /// <summary>The signals captured during the experiment, keyed by name.</summary>
        public IReadOnlyDictionary<string, object> Signals { get; }
        /// <summary>Notable events that occurred during the experiment.</summary>
        public IReadOnlyList<Event> Events { get; }
        /// <summary>The person or system that conducted the experiment.</summary>
        public string Operator { get; }
        /// <summary>Configuration parameters used for the experiment.</summary>
        public IReadOnlyDictionary<string, object> Configuration { get; }
        /// <summary>The start time of the experiment.</summary>
        public Timestamp Start { get; }
        /// <summary>The end time of the experiment (null if still running).</summary>
        public Timestamp? End { get; }
        /// <summary>Equipment or devices used in the experiment.</summary>
        public IReadOnlyList<string> Equipment { get; }
        /// <summary>Arbitrary tags for categorisation or filtering.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        /// <summary>Creates a new experiment.</summary>
        /// <param name="id">A unique identifier (required).</param>
        /// <param name="signals">Signals keyed by name.</param>
        /// <param name="events">Notable events.</param>
        /// <param name="operator">The operator name.</param>
        /// <param name="configuration">Configuration parameters.</param>
        /// <param name="start">Start timestamp.</param>
        /// <param name="end">End timestamp (optional).</param>
        /// <param name="equipment">List of equipment used.</param>
        /// <param name="tags">Arbitrary tags.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="id"/> is null.</exception>
        public Experiment(
            string id,
            IReadOnlyDictionary<string, object> signals = null,
            IReadOnlyList<Event> events = null,
            string @operator = null,
            IReadOnlyDictionary<string, object> configuration = null,
            Timestamp start = default,
            Timestamp? end = null,
            IReadOnlyList<string> equipment = null,
            IReadOnlyDictionary<string, string> tags = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Signals = signals ?? new Dictionary<string, object>();
            Events = events ?? Array.Empty<Event>();
            Operator = @operator ?? string.Empty;
            Configuration = configuration ?? new Dictionary<string, object>();
            Start = start;
            End = end;
            Equipment = equipment ?? Array.Empty<string>();
            Tags = tags ?? new Dictionary<string, string>();
        }

        /// <summary>Returns true if this experiment is equal to another by comparing IDs.</summary>
        /// <param name="other">The other experiment to compare against.</param>
        public bool Equals(Experiment other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        /// <summary>Returns true if this experiment is equal to another object.</summary>
        public override bool Equals(object obj) =>
            ReferenceEquals(this, obj) || (obj is Experiment other && Equals(other));

        /// <summary>Returns a hash code for this experiment.</summary>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>Returns true if two experiments are equal.</summary>
        public static bool operator ==(Experiment left, Experiment right) =>
            ReferenceEquals(left, right) || (left is null ? right is null : left.Equals(right));

        /// <summary>Returns true if two experiments are not equal.</summary>
        public static bool operator !=(Experiment left, Experiment right) => !(left == right);

        /// <summary>Returns a string representation of this experiment.</summary>
        public override string ToString() =>
            $"Experiment {Id} ({Start} - {End})";
    }
}
