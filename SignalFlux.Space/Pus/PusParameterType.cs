namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// The raw encoding of a parameter value as found on the wire in a PUS housekeeping parameter
    /// report. Integer encodings are two's complement, signed or unsigned as indicated; IEEE-754
    /// floats are big-endian.
    /// </summary>
    public enum PusParameterType
    {
        /// <summary>8-bit unsigned integer.</summary>
        UInt8 = 1,
        /// <summary>8-bit signed (two's complement) integer.</summary>
        Int8 = 2,
        /// <summary>16-bit unsigned integer.</summary>
        UInt16 = 3,
        /// <summary>16-bit signed (two's complement) integer.</summary>
        Int16 = 4,
        /// <summary>32-bit unsigned integer.</summary>
        UInt32 = 5,
        /// <summary>32-bit signed (two's complement) integer.</summary>
        Int32 = 6,
        /// <summary>32-bit IEEE-754 single-precision float.</summary>
        Float32 = 7,
        /// <summary>64-bit IEEE-754 double-precision float.</summary>
        Float64 = 8,
    }
}