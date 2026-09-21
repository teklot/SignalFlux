using System;

namespace SignalFlux.Space.Ccsds
{
    /// <summary>
    /// The CCSDS Space Packet packet-type field (bit 12 of the primary header): 0 = telemetry,
    /// 1 = command.
    /// </summary>
    public enum PacketType
    {
        /// <summary>Telemetry (TM) packet.</summary>
        Telemetry = 0,
        /// <summary>Command (TC) packet.</summary>
        Command = 1,
    }

    /// <summary>
    /// The CCSDS Space Packet grouping-flags field (bits 15-14 of the primary header), describing how
    /// the packet relates to the sequence of packets for the same application process.
    /// </summary>
    public enum GroupingFlags
    {
        /// <summary>Continuation segment of a packet sequence.</summary>
        ContinuationSegment = 0,
        /// <summary>First segment of a packet sequence.</summary>
        FirstSegment = 1,
        /// <summary>Last segment of a packet sequence.</summary>
        LastSegment = 2,
        /// <summary>Unsegmented packet (standalone).</summary>
        Unsegmented = 3,
    }

    /// <summary>
    /// A CCSDS Space Packet (CCSDS 133.0-B): the six-octet primary header plus the packet data field.
    /// All fields are read big-endian per CCSDS conventions. The packet data field is stored as an
    /// owned copy so the struct remains valid beyond the lifetime of the source buffer.
    /// </summary>
    public readonly struct SpacePacket : IEquatable<SpacePacket>
    {
        private static readonly byte[] EmptyData = Array.Empty<byte>();

        private readonly byte[] _dataField;

        private SpacePacket(
            byte version,
            PacketType type,
            bool hasSecondaryHeader,
            ushort apId,
            GroupingFlags groupingFlags,
            ushort sequenceCount,
            byte[] dataField)
        {
            Version = version;
            Type = type;
            HasSecondaryHeader = hasSecondaryHeader;
            ApId = apId;
            GroupingFlags = groupingFlags;
            SequenceCount = sequenceCount;
            _dataField = dataField;
        }

        /// <summary>The CCSDS version number (must be 0 for version-1 Space Packets).</summary>
        public byte Version { get; }

        /// <summary>The packet type: telemetry or command.</summary>
        public PacketType Type { get; }

        /// <summary>True when a secondary header is present at the start of the packet data field.</summary>
        public bool HasSecondaryHeader { get; }

        /// <summary>The 11-bit Application Process Identifier (APID).</summary>
        public ushort ApId { get; }

        /// <summary>The grouping flags describing the packet's place in a sequence.</summary>
        public GroupingFlags GroupingFlags { get; }

        /// <summary>The 14-bit packet sequence count or packet name.</summary>
        public ushort SequenceCount { get; }

        /// <summary>
        /// The packet data length field value, which equals the number of octets in the packet data
        /// field minus one.
        /// </summary>
        public int PacketDataLength => _dataField.Length - 1;

        /// <summary>The packet data field (secondary header plus source data, if any).</summary>
        public ReadOnlyMemory<byte> DataField => _dataField ?? EmptyData;

        /// <summary>
        /// Creates a version-1 Space Packet from its logical fields, with the packet data field copied
        /// into the packet.
        /// </summary>
        /// <param name="type">Telemetry or command.</param>
        /// <param name="apId">The 11-bit Application Process Identifier (0-0x7FF).</param>
        /// <param name="groupingFlags">The grouping flags.</param>
        /// <param name="sequenceCount">The 14-bit sequence count (0-0x3FFF).</param>
        /// <param name="dataField">The packet data field; at least one octet is required.</param>
        /// <param name="hasSecondaryHeader">True when a secondary header is present in the data field.</param>
        public static SpacePacket Create(
            PacketType type,
            ushort apId,
            GroupingFlags groupingFlags,
            ushort sequenceCount,
            ReadOnlySpan<byte> dataField,
            bool hasSecondaryHeader = false)
        {
            if (dataField.Length == 0)
                throw new ArgumentException(
                    "A CCSDS Space Packet requires at least one octet of packet data.", nameof(dataField));
            if (apId > 0x7FF)
                throw new ArgumentOutOfRangeException(nameof(apId), "APID must fit in 11 bits.");
            if (sequenceCount > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(sequenceCount), "Sequence count must fit in 14 bits.");

            return new SpacePacket(0, type, hasSecondaryHeader, apId, groupingFlags, sequenceCount, dataField.ToArray());
        }

        /// <summary>
        /// Decodes a complete CCSDS Space Packet from its raw octets.
        /// </summary>
        /// <param name="bytes">The complete packet, starting at the primary header.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the buffer is too short, the version is not zero, or the data-length field does
        /// not match the buffer length.
        /// </exception>
        public static SpacePacket Parse(ReadOnlySpan<byte> bytes)
        {
            if (!TryParse(bytes, out var packet))
                throw new InvalidOperationException("The buffer does not contain a valid CCSDS Space Packet.");
            return packet;
        }

        /// <summary>
        /// Attempts to decode a complete CCSDS Space Packet from its raw octets.
        /// </summary>
        /// <param name="bytes">The complete packet, starting at the primary header.</param>
        /// <param name="packet">The decoded packet when successful.</param>
        /// <returns>True when the buffer contains a valid Space Packet.</returns>
        public static bool TryParse(ReadOnlySpan<byte> bytes, out SpacePacket packet)
        {
            packet = default;
            if (bytes.Length < 7)
                return false;

            byte b0 = bytes[0];
            byte version = (byte)(b0 >> 5);
            if (version != 0)
                return false;

            var type = (PacketType)((b0 >> 4) & 0x1);
            bool hasSecondaryHeader = (b0 & 0x08) != 0;
            ushort apId = (ushort)(((b0 & 0x07) << 8) | bytes[1]);
            var groupingFlags = (GroupingFlags)((bytes[2] >> 6) & 0x3);
            ushort sequenceCount = (ushort)(((bytes[2] & 0x3F) << 8) | bytes[3]);
            int dataLength = (bytes[4] << 8) | bytes[5];

            if (dataLength + 1 != bytes.Length - 6)
                return false;

            packet = new SpacePacket(
                version,
                type,
                hasSecondaryHeader,
                apId,
                groupingFlags,
                sequenceCount,
                bytes.Slice(6, dataLength + 1).ToArray());
            return true;
        }

        /// <summary>Serializes the packet back to its raw octets (primary header plus data field).</summary>
        public byte[] ToBytes()
        {
            int dataLength = _dataField.Length;
            var buffer = new byte[6 + dataLength];
            buffer[0] = (byte)(
                ((Version & 0x7) << 5)
                | ((byte)Type << 4)
                | (HasSecondaryHeader ? 0x08 : 0)
                | ((ApId >> 8) & 0x07));
            buffer[1] = (byte)(ApId & 0xFF);
            buffer[2] = (byte)(((byte)GroupingFlags << 6) | ((SequenceCount >> 8) & 0x3F));
            buffer[3] = (byte)(SequenceCount & 0xFF);
            int lengthField = dataLength - 1;
            buffer[4] = (byte)(lengthField >> 8);
            buffer[5] = (byte)(lengthField & 0xFF);
            DataField.Span.CopyTo(buffer.AsSpan(6));
            return buffer;
        }

        /// <summary>Returns true when another packet has identical fields and data.</summary>
        public bool Equals(SpacePacket other) =>
            Version == other.Version
            && Type == other.Type
            && HasSecondaryHeader == other.HasSecondaryHeader
            && ApId == other.ApId
            && GroupingFlags == other.GroupingFlags
            && SequenceCount == other.SequenceCount
            && DataField.Span.SequenceEqual(other.DataField.Span);

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is SpacePacket other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + ApId.GetHashCode();
            hash = hash * 31 + (int)Type;
            hash = hash * 31 + SequenceCount.GetHashCode();
            return hash;
        }

        /// <summary>Returns true when two packets are equal.</summary>
        public static bool operator ==(SpacePacket left, SpacePacket right) => left.Equals(right);

        /// <summary>Returns true when two packets are not equal.</summary>
        public static bool operator !=(SpacePacket left, SpacePacket right) => !left.Equals(right);

        /// <summary>Returns a summary of the packet's identifying fields.</summary>
        public override string ToString() =>
            $"SpacePacket APID 0x{ApId:X3} {Type} seq {SequenceCount} ({DataField.Length} data octets)";
    }
}