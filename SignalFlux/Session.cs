using System;
using System.Collections.Generic;

namespace SignalFlux
{
    /// <summary>Groups multiple experiments and annotations into a cohesive testing or measurement session.</summary>
    public sealed class Session : IEquatable<Session>
    {
        /// <summary>A unique identifier for the session.</summary>
        public string Id { get; }
        /// <summary>The experiments conducted within this session.</summary>
        public IReadOnlyList<Experiment> Experiments { get; }
        /// <summary>Indicates whether the session can be replayed.</summary>
        public bool CanReplay { get; }
        /// <summary>Human-readable notes or annotations for the session.</summary>
        public IReadOnlyList<string> Annotations { get; }

        /// <summary>Creates a new session.</summary>
        /// <param name="id">A unique identifier (required).</param>
        /// <param name="experiments">Experiments in the session.</param>
        /// <param name="canReplay">Whether the session supports replay.</param>
        /// <param name="annotations">Human-readable annotations.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="id"/> is null.</exception>
        public Session(
            string id,
            IReadOnlyList<Experiment> experiments = null,
            bool canReplay = false,
            IReadOnlyList<string> annotations = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Experiments = experiments ?? Array.Empty<Experiment>();
            CanReplay = canReplay;
            Annotations = annotations ?? Array.Empty<string>();
        }

        /// <summary>Returns a new Session with the specified experiment appended.</summary>
        public Session WithExperiment(Experiment experiment)
        {
            var list = new List<Experiment>(Experiments) { experiment };
            return new Session(Id, list, CanReplay, Annotations);
        }

        /// <summary>Returns a new Session with the replay flag updated.</summary>
        public Session WithReplay(bool canReplay) =>
            new Session(Id, Experiments, canReplay, Annotations);

        /// <summary>Returns a new Session with the specified annotation appended.</summary>
        public Session WithAnnotation(string annotation)
        {
            var list = new List<string>(Annotations) { annotation };
            return new Session(Id, Experiments, CanReplay, list);
        }

        /// <summary>Returns true if this session is equal to another by comparing IDs.</summary>
        /// <param name="other">The other session to compare against.</param>
        public bool Equals(Session other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        /// <summary>Returns true if this session is equal to another object.</summary>
        public override bool Equals(object obj) =>
            ReferenceEquals(this, obj) || (obj is Session other && Equals(other));

        /// <summary>Returns a hash code for this session.</summary>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>Returns true if two sessions are equal.</summary>
        public static bool operator ==(Session left, Session right) =>
            ReferenceEquals(left, right) || (left is null ? right is null : left.Equals(right));

        /// <summary>Returns true if two sessions are not equal.</summary>
        public static bool operator !=(Session left, Session right) => !(left == right);

        /// <summary>Returns a string representation of this session.</summary>
        public override string ToString() =>
            $"Session {Id} ({Experiments.Count} experiments)";
    }
}
