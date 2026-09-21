using System;
using SignalFlux;
using SignalFlux.Space.Ccsds;

namespace SignalFlux.Space.Pus
{
    /// <summary>
    /// An ECSS PUS TM packet: a CCSDS <see cref="SpacePacket"/> whose secondary header carries the
    /// PUS protocol fields (PUS version, service type, service subtype, message counter, destination
    /// ID) followed by a CUC time field and the source data.
    /// </summary>
    public readonly struct PusPacket
    {
        /// <summary>The underlying CCSDS Space Packet.</summary>
        public SpacePacket Packet { get; }

        /// <summary>The PUS protocol version (high nibble of the first secondary-header octet).</summary>
        public byte PusVersion { get; }

        /// <summary>The spacecraft time-reference status (low nibble of the first secondary-header octet).</summary>
        public byte SpacecraftTimeReferenceStatus { get; }

        /// <summary>The PUS service type (1 octet).</summary>
        public byte ServiceType { get; }

        /// <summary>The PUS service subtype (1 octet).</summary>
        public byte ServiceSubtype { get; }

        /// <summary>The message type counter (1 octet).</summary>
        public byte MessageTypeCounter { get; }

        /// <summary>The destination ID (2 octets).</summary>
        public ushort DestinationId { get; }

        /// <summary>The packet time decoded from the CUC time field; null when absent.</summary>
        public Timestamp? Time { get; }

        /// <summary>The source data following the secondary header and time field.</summary>
        public ReadOnlyMemory<byte> SourceData { get; }

        /// <summary>The combined service type/subtype subservice identifier (0x0303 for TM[3,25]).</summary>
        public int Subservice => (ServiceType << 8) | ServiceSubtype;

        /// <summary>True when this is a PUS Service 3 (housekeeping) packet.</summary>
        public bool IsHousekeeping => ServiceType == 3;

        private PusPacket(
            SpacePacket packet,
            byte pusVersion,
            byte spacecraftTimeReferenceStatus,
            byte serviceType,
            byte serviceSubtype,
            byte messageTypeCounter,
            ushort destinationId,
            Timestamp? time,
            byte[] sourceData)
        {
            Packet = packet;
            PusVersion = pusVersion;
            SpacecraftTimeReferenceStatus = spacecraftTimeReferenceStatus;
            ServiceType = serviceType;
            ServiceSubtype = serviceSubtype;
            MessageTypeCounter = messageTypeCounter;
            DestinationId = destinationId;
            Time = time;
            SourceData = sourceData;
        }

        /// <summary>
        /// Decodes a PUS TM packet from raw octets, interpreting the secondary-header time field as a
        /// CUC time code relative to <paramref name="timeEpoch"/>.
        /// </summary>
        /// <param name="bytes">The complete packet starting at the primary header.</param>
        /// <param name="timeEpoch">The mission-defined epoch for the embedded CUC time field.</param>
        /// <param name="timeOctets">Total width of the CUC time field in octets (default 6 = CUC(32,16)).</param>
        /// <param name="fractionalBits">Number of sub-second bits in the time field (default 16).</param>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not a valid PUS TM packet.</exception>
        public static PusPacket Parse(ReadOnlySpan<byte> bytes, Timestamp timeEpoch, int timeOctets = 6, int fractionalBits = 16)
        {
            if (!TryParse(bytes, timeEpoch, timeOctets, fractionalBits, out var packet))
                throw new InvalidOperationException("The buffer does not contain a valid ECSS PUS TM packet.");
            return packet;
        }

        /// <summary>
        /// Attempts to decode a PUS TM packet from raw octets, interpreting the secondary-header time
        /// field as a CUC time code relative to <paramref name="timeEpoch"/>.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> bytes, Timestamp timeEpoch, int timeOctets, int fractionalBits, out PusPacket packet)
        {
            packet = default;
            if (!SpacePacket.TryParse(bytes, out var spacePacket))
                return false;
            return TryParse(spacePacket, timeEpoch, timeOctets, fractionalBits, out packet);
        }

        /// <summary>
        /// Attempts to decode a PUS TM packet from an already-parsed CCSDS <see cref="SpacePacket"/>.
        /// </summary>
        public static bool TryParse(SpacePacket spacePacket, Timestamp timeEpoch, int timeOctets, int fractionalBits, out PusPacket packet)
        {
            packet = default;
            if (spacePacket.Type != PacketType.Telemetry)
                return false;
            if (!spacePacket.HasSecondaryHeader)
                return false;

            int fractionalBytes = fractionalBits / 8;
            int coarseBytes = timeOctets - fractionalBytes;
            if (fractionalBits % 8 != 0 || fractionalBytes < 1 || fractionalBytes > 4 || coarseBytes < 1 || coarseBytes > 8)
                return false;

            ReadOnlySpan<byte> data = spacePacket.DataField.Span;
            if (data.Length < 6 + timeOctets)
                return false;

            byte b0 = data[0];
            byte pusVersion = (byte)(b0 >> 4);
            byte timeReferenceStatus = (byte)(b0 & 0x0F);
            byte serviceType = data[1];
            byte serviceSubtype = data[2];
            byte messageCounter = data[3];
            ushort destinationId = (ushort)((data[4] << 8) | data[5]);

            Timestamp? time;
            try
            {
                time = CucTime.Decode(data.Slice(6, timeOctets), timeEpoch, coarseBytes, fractionalBytes).ToTimestamp();
            }
            catch (ArgumentException)
            {
                return false;
            }

            packet = new PusPacket(
                spacePacket,
                pusVersion,
                timeReferenceStatus,
                serviceType,
                serviceSubtype,
                messageCounter,
                destinationId,
                time,
                data.Slice(6 + timeOctets).ToArray());
            return true;
        }

        /// <summary>
        /// Builds a PUS TM packet as a CCSDS <see cref="SpacePacket"/> with an embedded CUC time field,
        /// ready for streaming. The packet has the secondary-header flag set and telemetry type.
        /// </summary>
        /// <param name="timeEpoch">The mission-defined epoch for the embedded CUC time field.</param>
        /// <param name="apId">The 11-bit Application Process Identifier.</param>
        /// <param name="sequenceCount">The 14-bit packet sequence count.</param>
        /// <param name="serviceType">The PUS service type.</param>
        /// <param name="serviceSubtype">The PUS service subtype.</param>
        /// <param name="messageCounter">The message type counter.</param>
        /// <param name="destinationId">The destination ID.</param>
        /// <param name="time">The packet time to encode (must be at or after <paramref name="timeEpoch"/>).</param>
        /// <param name="sourceData">The source data (e.g. a housekeeping parameter report).</param>
        /// <param name="timeOctets">Total width of the CUC time field in octets (default 6 = CUC(32,16)).</param>
        /// <param name="fractionalBits">Number of sub-second bits in the time field (default 16).</param>
        public static SpacePacket CreateTm(
            Timestamp timeEpoch,
            ushort apId,
            ushort sequenceCount,
            byte serviceType,
            byte serviceSubtype,
            byte messageCounter,
            ushort destinationId,
            Timestamp time,
            ReadOnlySpan<byte> sourceData,
            int timeOctets = 6,
            int fractionalBits = 16)
        {
            int fractionalBytes = fractionalBits / 8;
            int coarseBytes = timeOctets - fractionalBytes;
            if (fractionalBits % 8 != 0 || fractionalBytes < 1 || fractionalBytes > 4 || coarseBytes < 1 || coarseBytes > 8)
                throw new ArgumentOutOfRangeException(nameof(fractionalBits), "Time field geometry is not a supported CUC layout.");

            double seconds = (time - timeEpoch).TotalSeconds;
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(time), "Packet time precedes the time epoch.");

            ulong coarse = (ulong)seconds;
            if ((coarse >> (coarseBytes * 8)) != 0)
                throw new ArgumentOutOfRangeException(nameof(time), "Packet time exceeds the CUC coarse-field range.");
            uint fine = (uint)Math.Round((seconds - coarse) * (1UL << fractionalBits), 0, MidpointRounding.AwayFromZero);
            if (fine >= (1UL << fractionalBits))
            {
                fine = 0;
                coarse++;
            }

            var buffer = new byte[6 + timeOctets + sourceData.Length];
            buffer[0] = 0x10; // PUS version 1, spacecraft time reference status 0
            buffer[1] = serviceType;
            buffer[2] = serviceSubtype;
            buffer[3] = messageCounter;
            buffer[4] = (byte)(destinationId >> 8);
            buffer[5] = (byte)(destinationId & 0xFF);

            for (int i = 0; i < coarseBytes; i++)
                buffer[6 + i] = (byte)(coarse >> (8 * (coarseBytes - 1 - i)));
            for (int i = 0; i < fractionalBytes; i++)
                buffer[6 + coarseBytes + i] = (byte)(fine >> (8 * (fractionalBytes - 1 - i)));

            sourceData.CopyTo(buffer.AsSpan(6 + timeOctets));

            return SpacePacket.Create(
                PacketType.Telemetry,
                apId,
                GroupingFlags.Unsegmented,
                sequenceCount,
                buffer,
                hasSecondaryHeader: true);
        }

        /// <summary>Returns a summary of the packet's identifying fields.</summary>
        public override string ToString() =>
            $"PUS TM Service {ServiceType}/{ServiceSubtype} msg {MessageTypeCounter} dest {DestinationId} APID 0x{Packet.ApId:X3}";
    }
}