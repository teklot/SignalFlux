using System.Text;
using SignalFlux;
using SignalFlux.Space.Ccsds;
using SignalFlux.Space.Pus;

namespace SignalFlux.Tests
{
    public class PusPacketTests
    {
        private static readonly Timestamp TimeEpoch = Timestamp.FromUnixSeconds(0);

        [Fact]
        public void CreateTm_AssemblesSecondaryHeaderAndTime()
        {
            var packetTime = TimeEpoch + TimeSpan.FromSeconds(120.25);
            var space = PusPacket.CreateTm(
                TimeEpoch,
                apId: 0x123,
                sequenceCount: 0x0A,
                serviceType: 3,
                serviceSubtype: 25,
                messageCounter: 7,
                destinationId: 0x0004,
                packetTime,
                new byte[] { 0x10, 0x20 });

            Assert.Equal(PacketType.Telemetry, space.Type);
            Assert.True(space.HasSecondaryHeader);

            Assert.True(PusPacket.TryParse(space.ToBytes(), TimeEpoch, 6, 16, out var pus));
            Assert.Equal(1, pus.PusVersion);
            Assert.Equal(0, pus.SpacecraftTimeReferenceStatus);
            Assert.Equal(3, pus.ServiceType);
            Assert.Equal(25, pus.ServiceSubtype);
            Assert.Equal(7, pus.MessageTypeCounter);
            Assert.Equal(0x0004, pus.DestinationId);
            Assert.Equal(0x0319, pus.Subservice);
            Assert.True(pus.IsHousekeeping);
            Assert.Equal(packetTime.Ticks, pus.Time!.Value.Ticks);
            Assert.Equal(new byte[] { 0x10, 0x20 }, pus.SourceData.ToArray());
        }

        [Fact]
        public void TryParse_CommandPacket_Rejected()
        {
            var space = SpacePacket.Create(
                PacketType.Command,
                apId: 0x123,
                GroupingFlags.Unsegmented,
                0,
                new byte[] { 0x10, 0x03, 0x19, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

            Assert.False(PusPacket.TryParse(space.ToBytes(), TimeEpoch, 6, 16, out _));
        }

        [Fact]
        public void TryParse_MissingSecondaryHeader_Rejected()
        {
            var space = SpacePacket.Create(PacketType.Telemetry, 0x123, GroupingFlags.Unsegmented, 0, new byte[] { 0x01 });

            Assert.False(PusPacket.TryParse(space.ToBytes(), TimeEpoch, 6, 16, out _));
        }

        [Fact]
        public void TryParse_InvalidTimeGeometry_Rejected()
        {
            var space = PusPacket.CreateTm(
                TimeEpoch, 0x123, 0, 3, 25, 0, 0x0004, TimeEpoch, new byte[] { 0x01 });

            // fractionalBits must be a multiple of 8 and at least one fractional octet wide.
            Assert.False(PusPacket.TryParse(space.ToBytes(), TimeEpoch, 6, 4, out _));
        }

        [Fact]
        public void Crc16_KnownCheckValue()
        {
            // The classic CRC-16/CCITT-FALSE check string.
            Assert.Equal(0x29B1u, Crc16.Compute(Encoding.ASCII.GetBytes("123456789")));
        }

        [Fact]
        public void Crc16_VerifiesWholePacket()
        {
            var bytes = PusPacket.CreateTm(TimeEpoch, 0x123, 0, 3, 25, 0, 0x0004, TimeEpoch, new byte[] { 0x01 }).ToBytes();

            ushort crc = Crc16.Compute(bytes);
            Assert.True(Crc16.Verify(bytes, crc));

            bytes[0] ^= 0x01; // corrupt the primary header
            Assert.False(Crc16.Verify(bytes, crc));
        }

        [Fact]
        public void Parse_RoundTripsEpochTime()
        {
            var packetTime = TimeEpoch + TimeSpan.FromSeconds(10.5);
            var space = PusPacket.CreateTm(TimeEpoch, 0x123, 0, 3, 26, 1, 0x0004, packetTime, new byte[] { 0x00, 0x2A });

            var pus = PusPacket.Parse(space.ToBytes(), TimeEpoch);
            Assert.Equal(10_500, pus.Time!.Value.ToUnixMilliseconds());
        }
    }
}