using System;
using System.Collections.Generic;
using SignalFlux;

namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// Decodes the source data of a PUS Service 3 housekeeping packet (TM[3,25] plain parameter report,
    /// or the TM[3,26] SID-qualified variant) into <see cref="Measurement{T}"/> values using a set of
    /// <see cref="PusParameterDefinition"/> entries.
    /// </summary>
    /// <remarks>
    /// The report body is a sequence of one-octet parameter identifiers followed by the raw value
    /// octets of the declared type. Because the width of a value is dictated by its definition, every
    /// parameter id present on the wire must have a definition; decoding throws when an unknown id is
    /// encountered rather than guessing the width to skip.
    /// </remarks>
    public sealed class PusHousekeepingDecoder
    {
        private readonly Dictionary<byte, PusParameterDefinition> _definitions;

        /// <summary>All parameter definitions known to this decoder.</summary>
        public IReadOnlyCollection<PusParameterDefinition> Definitions => _definitions.Values;

        /// <summary>Creates a decoder for the given parameter definitions.</summary>
        /// <param name="definitions">The parameter definitions covering every parameter id in the reports.</param>
        public PusHousekeepingDecoder(IReadOnlyList<PusParameterDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = new Dictionary<byte, PusParameterDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Parameter definitions must not be null.", nameof(definitions));
                if (_definitions.ContainsKey(definition.Id))
                    throw new ArgumentException($"Duplicate parameter id 0x{definition.Id:X2}.", nameof(definitions));
                _definitions[definition.Id] = definition;
            }
        }

        /// <summary>
        /// Decodes a housekeeping parameter report from a PUS TM packet's source data.
        /// </summary>
        /// <param name="packet">The PUS TM packet carrying the report.</param>
        /// <param name="sidIncluded">True when the source data begins with a 2-octet SID (TM[3,26] variant).</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body is truncated or contains an undecodable parameter.
        /// </exception>
        public PusHousekeepingReport Decode(PusPacket packet, bool sidIncluded = false)
        {
            ReadOnlySpan<byte> source = packet.SourceData.Span;

            int offset = 0;
            ushort? sid = null;
            if (sidIncluded)
            {
                if (source.Length < 2)
                    throw new InvalidOperationException("SID-qualified report is shorter than the 2-octet SID.");
                sid = (ushort)((source[0] << 8) | source[1]);
                offset = 2;
            }

            var metadataBase = new Metadata()
                .With("source", "pus")
                .With("apid", "0x" + packet.Packet.ApId.ToString("X3"))
                .With("service_type", packet.ServiceType.ToString())
                .With("service_subtype", packet.ServiceSubtype.ToString())
                .With("message_counter", packet.MessageTypeCounter.ToString());
            if (sid.HasValue)
                metadataBase = metadataBase.With("sid", sid.Value.ToString());

            var measurements = new List<Measurement<double>>();
            while (offset < source.Length)
            {
                byte id = source[offset++];
                if (!_definitions.TryGetValue(id, out var definition))
                    throw new InvalidOperationException($"Unknown PUS parameter id 0x{id:X2}; provide a definition to decode it.");

                int valueBytes = SizeOf(definition.Type);
                if (source.Length - offset < valueBytes)
                    throw new InvalidOperationException($"Parameter 0x{id:X2} value is truncated in the report.");

                double physical = DecodeRaw(source.Slice(offset, valueBytes), definition);
                offset += valueBytes;

                Quality quality =
                    definition.HasRange && (physical < definition.Minimum || physical > definition.Maximum)
                        ? Quality.Bad
                        : Quality.Good;

                var metadata = metadataBase
                    .With("pus_parameter_id", id)
                    .With("pus_parameter", definition.Name);

                measurements.Add(new Measurement<double>(
                    physical,
                    packet.Time ?? Timestamp.Now,
                    definition.Unit,
                    quality,
                    metadata));
            }

            return new PusHousekeepingReport(sid, measurements);
        }

        /// <summary>
        /// Detaches a raw value from the report for encoding scenarios. Provided so callers can build a
        /// report body by appending the parameter id and the wire-encoded raw value.
        /// </summary>
        /// <param name="value">The physical value.</param>
        /// <param name="definition">The parameter definition describing the encoding.</param>
        /// <returns>The wire-encoded raw octets (big-endian, two's complement for signed integers).</returns>
        public static byte[] EncodeValue(double value, PusParameterDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            double raw = (value - definition.Offset) / definition.Scale;
            switch (definition.Type)
            {
                case PusParameterType.UInt8:
                    return new[] { (byte)Math.Round(raw, 0, MidpointRounding.AwayFromZero) };
                case PusParameterType.Int8:
                    return new[] { unchecked((byte)(sbyte)Math.Round(raw, 0, MidpointRounding.AwayFromZero)) };
                case PusParameterType.UInt16:
                    return BigEndian((ushort)Math.Round(raw, 0, MidpointRounding.AwayFromZero));
                case PusParameterType.Int16:
                    return BigEndian(unchecked((ushort)(short)Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
                case PusParameterType.UInt32:
                    return BigEndian((uint)Math.Round(raw, 0, MidpointRounding.AwayFromZero));
                case PusParameterType.Int32:
                    return BigEndian(unchecked((uint)(int)Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
                case PusParameterType.Float32:
                    return BigEndian(BitConverter.GetBytes((float)raw));
                case PusParameterType.Float64:
                    return BigEndian(BitConverter.GetBytes(raw));
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition));
            }
        }

        private static double DecodeRaw(ReadOnlySpan<byte> raw, PusParameterDefinition definition)
        {
            switch (definition.Type)
            {
                case PusParameterType.UInt8:
                    return raw[0] * definition.Scale + definition.Offset;
                case PusParameterType.Int8:
                    return unchecked((sbyte)raw[0]) * definition.Scale + definition.Offset;
                case PusParameterType.UInt16:
                    return ((raw[0] << 8) | raw[1]) * definition.Scale + definition.Offset;
                case PusParameterType.Int16:
                    return unchecked((short)((raw[0] << 8) | raw[1])) * definition.Scale + definition.Offset;
                case PusParameterType.UInt32:
                    return ((((uint)raw[0] << 24) | ((uint)raw[1] << 16) | ((uint)raw[2] << 8) | raw[3])) * definition.Scale + definition.Offset;
                case PusParameterType.Int32:
                    return unchecked((int)(((uint)raw[0] << 24) | ((uint)raw[1] << 16) | ((uint)raw[2] << 8) | raw[3])) * definition.Scale + definition.Offset;
                case PusParameterType.Float32:
                    return DecodeFloat32(raw) * definition.Scale + definition.Offset;
                case PusParameterType.Float64:
                    return DecodeFloat64(raw) * definition.Scale + definition.Offset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition));
            }
        }

        private static float DecodeFloat32(ReadOnlySpan<byte> raw)
        {
            var bytes = raw.ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static double DecodeFloat64(ReadOnlySpan<byte> raw)
        {
            var bytes = raw.ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        private static byte[] BigEndian(ushort value) =>
            new[] { (byte)(value >> 8), (byte)(value & 0xFF) };

        private static byte[] BigEndian(uint value) =>
            new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };

        private static byte[] BigEndian(byte[] littleEndianBytes)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(littleEndianBytes);
            return littleEndianBytes;
        }

        private static int SizeOf(PusParameterType type) =>
            type switch
            {
                PusParameterType.UInt8 => 1,
                PusParameterType.Int8 => 1,
                PusParameterType.UInt16 => 2,
                PusParameterType.Int16 => 2,
                PusParameterType.UInt32 => 4,
                PusParameterType.Int32 => 4,
                PusParameterType.Float32 => 4,
                PusParameterType.Float64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
    }
}