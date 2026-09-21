using SignalFlux.Space.Ccsds;

namespace SignalFlux.Tests
{
    public class SpacePacketTests
    {
        // Telemetry, no secondary header, APID 0x123, unsegmented, seq 0x0A, L=1, data {0xDE, 0xAD}.
        private static readonly byte[] RawPacket =
            { 0x01, 0x23, 0xC0, 0x0A, 0x00, 0x01, 0xDE, 0xAD };

        [Fact]
        public void Parse_ExtractsPrimaryHeaderFields()
        {
            var packet = SpacePacket.Parse(RawPacket);

            Assert.Equal(0, packet.Version);
            Assert.Equal(PacketType.Telemetry, packet.Type);
            Assert.False(packet.HasSecondaryHeader);
            Assert.Equal(0x123u, packet.ApId);
            Assert.Equal(GroupingFlags.Unsegmented, packet.GroupingFlags);
            Assert.Equal(0x0Au, packet.SequenceCount);
            Assert.Equal(1, packet.PacketDataLength);
            Assert.Equal(new byte[] { 0xDE, 0xAD }, packet.DataField.ToArray());
        }

        [Fact]
        public void Create_ToBytes_RoundTrips()
        {
            var packet = SpacePacket.Create(
                PacketType.Telemetry,
                apId: 0x123,
                GroupingFlags.Unsegmented,
                sequenceCount: 0x0A,
                new byte[] { 0xDE, 0xAD });

            Assert.True(SpacePacket.TryParse(packet.ToBytes(), out var decoded));
            Assert.Equal(packet, decoded);
            Assert.Equal(RawPacket, decoded.ToBytes());
        }

        [Fact]
        public void Create_TMSecondaryHeader_EncodesFlag()
        {
            var packet = SpacePacket.Create(
                PacketType.Telemetry,
                apId: 0x321,
                GroupingFlags.FirstSegment,
                sequenceCount: 1,
                new byte[] { 0x10, 0x03, 0x19, 0x01, 0x00, 0x04 },
                hasSecondaryHeader: true);

            Assert.True(packet.HasSecondaryHeader);
            Assert.Equal(0x0Bu, packet.ToBytes()[0]); // 000 0 1 011 (APID high 3 bits)
            Assert.Equal(0x40u, packet.ToBytes()[2]); // 01 000000
        }

        [Fact]
        public void TryParse_TooShort_ReturnsFalse()
        {
            Assert.False(SpacePacket.TryParse(new byte[] { 0x01, 0x23, 0xC0, 0x0A, 0x00, 0x01 }, out _));
        }

        [Fact]
        public void TryParse_UnsupportedVersion_ReturnsFalse()
        {
            var bad = (byte[])RawPacket.Clone();
            bad[0] = 0x20; // version 1
            Assert.False(SpacePacket.TryParse(bad, out _));
        }

        [Fact]
        public void TryParse_LengthFieldMismatch_ReturnsFalse()
        {
            var bad = (byte[])RawPacket.Clone();
            bad[5] = 0x02; // claims 3 data octets, buffer only has 2
            Assert.False(SpacePacket.TryParse(bad, out _));
        }

        [Fact]
        public void Parse_Malformed_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                SpacePacket.Parse(new byte[] { 0x00 }));
        }

        [Fact]
        public void Create_EmptyDataField_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SpacePacket.Create(PacketType.Telemetry, 0x123, GroupingFlags.Unsegmented, 0, System.ReadOnlySpan<byte>.Empty));
        }

        [Fact]
        public void Create_OutOfRangeApId_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                SpacePacket.Create(PacketType.Telemetry, 0x800, GroupingFlags.Unsegmented, 0, new byte[] { 0x00 }));
        }
    }
}