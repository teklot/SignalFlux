using System;
using Opc.Ua;

namespace SignalFlux.Protocols.OpcUa
{
    /// <summary>Extension methods for converting OPC UA <see cref="DataValue"/> to SignalFlux <see cref="Measurement{T}"/> and <see cref="Quality"/>.</summary>
    public static class OpcUaSignalExtensions
    {
        /// <summary>Converts an OPC UA <see cref="DataValue"/> to a SignalFlux <see cref="Measurement{T}"/>.</summary>
        /// <param name="dataValue">The OPC UA data value to convert.</param>
        /// <param name="source">Source identifier for the measurement.</param>
        /// <returns>A <see cref="Measurement{T}"/> with the value, timestamp, and quality from the data value.</returns>
        public static Measurement<double> ToMeasurement(
            this DataValue dataValue,
            string source = "opcua")
        {
            if (dataValue == null) throw new ArgumentNullException(nameof(dataValue));

            double value = Convert.ToDouble(dataValue.WrappedValue.Value);
            Timestamp timestamp = dataValue.SourceTimestamp.Kind != DateTimeKind.Utc
                ? Timestamp.FromDateTime(dataValue.SourceTimestamp.ToUniversalTime())
                : Timestamp.FromDateTime(dataValue.SourceTimestamp);
            Quality quality = dataValue.StatusCode.ToQuality();

            return new Measurement<double>(value, timestamp, quality: quality,
                metadata: new Metadata().With("source", source));
        }

        /// <summary>Converts an OPC UA <see cref="StatusCode"/> to a SignalFlux <see cref="Quality"/>.</summary>
        /// <param name="statusCode">The OPC UA status code.</param>
        /// <returns>The corresponding quality level.</returns>
        public static Quality ToQuality(this StatusCode statusCode)
        {
            uint code = statusCode.Code;

            if (code == 0) return Quality.Good;

            // Uncertain range: 0x4000 - 0x7FFF
            if (code >= 0x4000 && code <= 0x7FFF) return Quality.Fair;

            // Bad range: 0x8000 - 0xFFFF
            if (code >= 0x8000) return Quality.Bad;

            // Non-zero good-ish codes (e.g., informational) treat as good
            return Quality.Good;
        }
    }
}
