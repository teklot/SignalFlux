using System;
using System.Linq;
using NmeaParser;
using NmeaParser.Messages;

namespace SignalFlux.Protocols.Nmea
{
    /// <summary>Extension methods for converting parsed NMEA sentences into <see cref="Measurement{T}"/> values.</summary>
    public static class NmeaSentenceExtensions
    {
        /// <summary>Extracts a field from an NMEA RMC (Recommended Minimum) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="rmc">The parsed RMC sentence.</param>
        /// <param name="fieldName">The field to extract: "latitude", "longitude", "speed", "course".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Rmc rmc, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.FromDateTime(rmc.FixTime.DateTime);
            switch (fieldName.ToLowerInvariant())
            {
                case "latitude": return new Measurement<double>(rmc.Latitude, ts);
                case "longitude": return new Measurement<double>(rmc.Longitude, ts);
                case "speed": return new Measurement<double>(rmc.Speed, ts);
                case "course": return new Measurement<double>(rmc.Course, ts);
                default: throw new ArgumentException($"Unknown RMC field: '{fieldName}'. Valid: latitude, longitude, speed, course.", nameof(fieldName));
            }
        }

        /// <summary>Extracts a field from an NMEA GGA (GPS Fix) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="gga">The parsed GGA sentence.</param>
        /// <param name="fieldName">The field to extract: "latitude", "longitude", "altitude", "hdop", "satellites".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Gga gga, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.FromDateTime(DateTime.UtcNow.Date.Add(gga.FixTime));
            switch (fieldName.ToLowerInvariant())
            {
                case "latitude": return new Measurement<double>(gga.Latitude, ts);
                case "longitude": return new Measurement<double>(gga.Longitude, ts);
                case "altitude": return new Measurement<double>(gga.Altitude, ts);
                case "hdop": return new Measurement<double>(gga.Hdop, ts);
                case "satellites": return new Measurement<double>(gga.NumberOfSatellites, ts);
                default: throw new ArgumentException($"Unknown GGA field: '{fieldName}'. Valid: latitude, longitude, altitude, hdop, satellites.", nameof(fieldName));
            }
        }

        /// <summary>Extracts a field from an NMEA VTG (Track and Ground Speed) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="vtg">The parsed VTG sentence.</param>
        /// <param name="fieldName">The field to extract: "course", "speed-knots", "speed-kph".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Vtg vtg, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.Now;
            switch (fieldName.ToLowerInvariant())
            {
                case "course": return new Measurement<double>(vtg.CourseTrue, ts);
                case "speed-knots": return new Measurement<double>(vtg.SpeedKnots, ts);
                case "speed-kph": return new Measurement<double>(vtg.SpeedKph, ts);
                default: throw new ArgumentException($"Unknown VTG field: '{fieldName}'. Valid: course, speed-knots, speed-kph.", nameof(fieldName));
            }
        }

        /// <summary>Extracts a field from an NMEA GSA (DOP and Active Satellites) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="gsa">The parsed GSA sentence.</param>
        /// <param name="fieldName">The field to extract: "pdop", "hdop", "vdop".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Gsa gsa, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.Now;
            switch (fieldName.ToLowerInvariant())
            {
                case "pdop": return new Measurement<double>(gsa.Pdop, ts);
                case "hdop": return new Measurement<double>(gsa.Hdop, ts);
                case "vdop": return new Measurement<double>(gsa.Vdop, ts);
                default: throw new ArgumentException($"Unknown GSA field: '{fieldName}'. Valid: pdop, hdop, vdop.", nameof(fieldName));
            }
        }

        /// <summary>Extracts a field from an NMEA GSV (Satellites in View) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="gsv">The parsed GSV sentence.</param>
        /// <param name="fieldName">The field to extract: "satellites-in-view", "signal-noise-ratio".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Gsv gsv, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.Now;
            switch (fieldName.ToLowerInvariant())
            {
                case "satellites-in-view": return new Measurement<double>(gsv.SatellitesInView, ts);
                case "signal-noise-ratio":
                    var snrValues = gsv.SVs.Select(s => (double)s.SignalToNoiseRatio).ToArray();
                    var avgSnr = snrValues.Length > 0 ? snrValues.Average() : 0.0;
                    return new Measurement<double>(avgSnr, ts);
                default: throw new ArgumentException($"Unknown GSV field: '{fieldName}'. Valid: satellites-in-view, signal-noise-ratio.", nameof(fieldName));
            }
        }

        /// <summary>Extracts a field from an NMEA GLL (Geographic Position) sentence as a <see cref="Measurement{T}"/>.</summary>
        /// <param name="gll">The parsed GLL sentence.</param>
        /// <param name="fieldName">The field to extract: "latitude", "longitude".</param>
        /// <param name="source">Source identifier (default "nmea").</param>
        /// <returns>A <see cref="Measurement{T}"/> representing the extracted field value.</returns>
        public static Measurement<double> ToMeasurement(this Gll gll, string fieldName, string source = "nmea")
        {
            var ts = Timestamp.FromDateTime(DateTime.UtcNow.Date.Add(gll.FixTime));
            switch (fieldName.ToLowerInvariant())
            {
                case "latitude": return new Measurement<double>(gll.Latitude, ts);
                case "longitude": return new Measurement<double>(gll.Longitude, ts);
                default: throw new ArgumentException($"Unknown GLL field: '{fieldName}'. Valid: latitude, longitude.", nameof(fieldName));
            }
        }
    }
}
