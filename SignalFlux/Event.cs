using System;

namespace SignalFlux
{
    /// <summary>Represents a notable occurrence at a specific time during an experiment or session.</summary>
    public readonly struct Event : IEquatable<Event>
    {
        /// <summary>The time at which the event occurred.</summary>
        public Timestamp Time { get; }
        /// <summary>The severity level of the event.</summary>
        public EventSeverity Severity { get; }
        /// <summary>A machine-readable classification label (e.g., "SensorGlitch", "Calibration").</summary>
        public string Type { get; }
        /// <summary>A human-readable description of the event.</summary>
        public string Description { get; }
        /// <summary>An optional identifier for the source component that generated the event.</summary>
        public string Source { get; }

        /// <summary>Creates an event.</summary>
        /// <param name="time">The time of occurrence.</param>
        /// <param name="severity">The severity level.</param>
        /// <param name="type">A machine-readable classification label.</param>
        /// <param name="description">A human-readable description.</param>
        /// <param name="source">An optional source identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
        public Event(
            Timestamp time,
            EventSeverity severity,
            string type,
            string description,
            string source = null)
        {
            Time = time;
            Severity = severity;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Description = description ?? string.Empty;
            Source = source ?? string.Empty;
        }

        /// <summary>Returns true if this event is equal to another by comparing all fields.</summary>
        /// <param name="other">The other event to compare against.</param>
        public bool Equals(Event other) =>
            Time.Equals(other.Time) &&
            Severity == other.Severity &&
            Type == other.Type &&
            Description == other.Description &&
            Source == other.Source;

        /// <summary>Returns true if this event is equal to another object.</summary>
        public override bool Equals(object obj) =>
            obj is Event other && Equals(other);

        /// <summary>Returns a hash code for this event.</summary>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Time.GetHashCode();
            hash = hash * 31 + (int)Severity;
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            return hash;
        }

        /// <summary>Returns true if two events are equal.</summary>
        public static bool operator ==(Event left, Event right) => left.Equals(right);
        /// <summary>Returns true if two events are not equal.</summary>
        public static bool operator !=(Event left, Event right) => !left.Equals(right);

        /// <summary>Returns a string representation of this event.</summary>
        public override string ToString() =>
            $"[{Severity}] {Type}: {Description} @ {Time}";
    }

    /// <summary>Defines severity levels for events.</summary>
    public enum EventSeverity
    {
        /// <summary>Verbose diagnostic information.</summary>
        Debug = 0,
        /// <summary>Normal operational notification.</summary>
        Info = 1,
        /// <summary>An unexpected condition that does not interrupt operation.</summary>
        Warning = 2,
        /// <summary>A significant problem that may affect results.</summary>
        Error = 3,
        /// <summary>A catastrophic failure requiring immediate attention.</summary>
        Critical = 4
    }
}
