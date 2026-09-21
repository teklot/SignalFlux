using System.Collections.Generic;
using SignalFlux;

namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// The result of decoding a PUS Service 3 housekeeping parameter report: the optional Structure
    /// Identifier (SID) and the decoded parameters as <see cref="Measurement{T}"/> values.
    /// </summary>
    public sealed class PusHousekeepingReport
    {
        /// <summary>The 2-octet Structure Identifier, when the report carries one (TM[3,26] variant).</summary>
        public ushort? SID { get; }

        /// <summary>The decoded parameters, one measurement per parameter.</summary>
        public IReadOnlyList<Measurement<double>> Measurements { get; }

        /// <summary>Creates a housekeeping report.</summary>
        /// <param name="sid">The Structure Identifier, when present.</param>
        /// <param name="measurements">The decoded parameter measurements.</param>
        public PusHousekeepingReport(ushort? sid, IReadOnlyList<Measurement<double>> measurements)
        {
            SID = sid;
            Measurements = measurements ?? throw new System.ArgumentNullException(nameof(measurements));
        }
    }
}