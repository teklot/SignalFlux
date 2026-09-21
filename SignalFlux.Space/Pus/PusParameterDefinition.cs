using System;

namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// Describes how one parameter in a PUS housekeeping parameter report is interpreted: its wire
    /// encoding, engineering unit, and the scale/offset mapping from raw to physical value.
    /// </summary>
    public sealed class PusParameterDefinition
    {
        /// <summary>The one-octet parameter identifier used on the wire.</summary>
        public byte Id { get; }

        /// <summary>The human-readable parameter name.</summary>
        public string Name { get; }

        /// <summary>The raw wire encoding of the parameter value.</summary>
        public PusParameterType Type { get; }

        /// <summary>The engineering unit of the physical value (a UnitsNet unit enum); null when untyped.</summary>
        public Enum? Unit { get; }

        /// <summary>The multiplier applied to the raw value (default 1).</summary>
        public double Scale { get; }

        /// <summary>The additive offset applied after scaling (default 0).</summary>
        public double Offset { get; }

        /// <summary>Lower bound of the physical range; measure quality when set. NaN when unused.</summary>
        public double Minimum { get; }

        /// <summary>Upper bound of the physical range; measure quality when set. NaN when unused.</summary>
        public double Maximum { get; }

        /// <summary>
        /// Creates a parameter definition.
        /// </summary>
        /// <param name="id">The one-octet parameter identifier.</param>
        /// <param name="name">The human-readable parameter name.</param>
        /// <param name="type">The raw wire encoding.</param>
        /// <param name="unit">The engineering unit (a UnitsNet unit enum); null when untyped.</param>
        /// <param name="scale">The raw-to-physical multiplier (default 1).</param>
        /// <param name="offset">The raw-to-physical additive offset (default 0).</param>
        /// <param name="minimum">Lower bound of the physical range (quality check).</param>
        /// <param name="maximum">Upper bound of the physical range (quality check).</param>
        public PusParameterDefinition(
            byte id,
            string name,
            PusParameterType type,
            Enum? unit = null,
            double scale = 1.0,
            double offset = 0.0,
            double minimum = double.NaN,
            double maximum = double.NaN)
        {
            if (scale == 0) throw new ArgumentOutOfRangeException(nameof(scale));
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Unit = unit;
            Scale = scale;
            Offset = offset;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>True when a physical range is defined (both bounds finite and ordered).</summary>
        public bool HasRange =>
            !double.IsNaN(Minimum)
            && !double.IsInfinity(Minimum)
            && !double.IsNaN(Maximum)
            && !double.IsInfinity(Maximum)
            && Minimum < Maximum;
    }
}