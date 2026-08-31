using SignalFlux.Protocols.Can;

namespace SignalFlux.Protocols.Can.Dbc
{
    /// <summary>
    /// A single CAN signal definition parsed from a DBC file. Captures the bit layout, scaling,
    /// value bounds, unit, and value-type metadata needed to decode a raw frame into a physical value.
    /// </summary>
    public sealed class DbcSignal
    {
        /// <summary>The signal name.</summary>
        public string Name { get; set; }

        /// <summary>Whether the signal is multiplexed and, if so, its multiplex indicator ("M" for a
        /// multiplexor, "mN" for a multiplexed member with switch value N). Null for non-multiplexed.</summary>
        public string MultiplexerIndicator { get; set; }

        /// <summary>The DBC start bit (0-based).</summary>
        public int StartBit { get; set; }

        /// <summary>The signal width in bits.</summary>
        public int Length { get; set; }

        /// <summary>The byte ordering of the signal.</summary>
        public CanByteOrder ByteOrder { get; set; }

        /// <summary>Whether the value is signed (two's complement).</summary>
        public bool IsSigned { get; set; }

        /// <summary>The scaling factor (raw * factor + offset).</summary>
        public double Factor { get; set; } = 1.0;

        /// <summary>The offset (raw * factor + offset).</summary>
        public double Offset { get; set; }

        /// <summary>The physical minimum value.</summary>
        public double Minimum { get; set; }

        /// <summary>The physical maximum value.</summary>
        public double Maximum { get; set; }

        /// <summary>The engineering unit string.</summary>
        public string Unit { get; set; }

        /// <summary>Optional value-to-name mapping (e.g., 0 = "OFF", 1 = "ON").</summary>
        public System.Collections.Generic.Dictionary<ulong, string> ValueDescriptions { get; } =
            new System.Collections.Generic.Dictionary<ulong, string>();

        /// <summary>Whether this signal acts as a multiplexor of the message.</summary>
        public bool IsMultiplexor =>
            !string.IsNullOrEmpty(MultiplexerIndicator) &&
            MultiplexerIndicator == "M";

        /// <summary>Whether this signal is a multiplexed member (not the multiplexor).</summary>
        public bool IsMultiplexed =>
            !string.IsNullOrEmpty(MultiplexerIndicator) &&
            MultiplexerIndicator != "M";

        /// <summary>The switch value under which a multiplexed signal is active (null for the multiplexor).</summary>
        public int? MultiplexerSwitchValue => IsMultiplexed
            ? int.Parse(MultiplexerIndicator.Substring(1))
            : (int?)null;

        /// <summary>Returns a readable description of the signal.</summary>
        public override string ToString() =>
            $"{Name} [{StartBit}:{Length} {(ByteOrder == CanByteOrder.LittleEndian ? "LE" : "BE")}] " +
            $"{Factor:g}× +{Offset:g} ({Minimum:g}..{Maximum:g}) {Unit}";
    }
}
