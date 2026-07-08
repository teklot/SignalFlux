namespace SignalFlux
{
    /// <summary>Indicates the quality or confidence level of a signal, measurement, or data point.</summary>
    public enum Quality
    {
        /// <summary>Quality has not been assessed or is unspecified.</summary>
        Unknown = 0,
        /// <summary>Data is within expected tolerances and suitable for analysis.</summary>
        Good = 1,
        /// <summary>Data is usable but may have minor anomalies or reduced confidence.</summary>
        Fair = 2,
        /// <summary>Data is degraded and should be used with caution.</summary>
        Poor = 3,
        /// <summary>Data is unreliable and should typically be excluded from analysis.</summary>
        Bad = 4,
        /// <summary>Data is physically or logically impossible; must be discarded.</summary>
        Invalid = 5
    }
}
