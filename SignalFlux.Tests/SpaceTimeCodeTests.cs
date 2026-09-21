using System;
using SignalFlux;
using SignalFlux.Space.Ccsds;

namespace SignalFlux.Tests
{
    public class SpaceTimeCodeTests
    {
        private static readonly Timestamp UnixEpoch = Timestamp.FromUnixSeconds(0);

        [Fact]
        public void Cuc_DecodesKnownField()
        {
            // CUC(32,16): coarse 10 s, fine 0x8000 (2^15 units of 2^-16 s = 0.5 s).
            var time = CucTime.Decode(new byte[] { 0x00, 0x00, 0x00, 0x0A, 0x80, 0x00 }, UnixEpoch);

            Assert.Equal(10uL, time.Coarse);
            Assert.Equal(0x8000u, time.Fine);
            Assert.Equal(16, time.SubSecondBits);
            Assert.Equal(10_500, time.ToTimestamp().ToUnixMilliseconds());

            var expected = UnixEpoch + TimeSpan.FromSeconds(10.5);
            Assert.Equal(expected.Ticks, time.ToTimestamp().Ticks);
        }

        [Fact]
        public void Cuc_DecodeWideCoarse()
        {
            // CUC(64,16): coarse 345 s, fine 0x0001 (2^-16 s).
            var time = CucTime.Decode(
                new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x59, 0x00, 0x01 },
                UnixEpoch,
                coarseBytes: 8,
                fractionalBytes: 2);

            Assert.Equal(345, time.ToTimestamp().ToUnixSeconds());
        }

        [Fact]
        public void Cuc_WrongFieldWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CucTime.Decode(new byte[] { 0x00, 0x00, 0x00 }, UnixEpoch));
        }

        [Fact]
        public void Cuc_FineOverflow_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CucTime(UnixEpoch, 0, 0x10000, 16));
        }

        [Fact]
        public void Cds_DecodesSevenOctetField()
        {
            // days 1, millisOfDay 300, subMilli 0x8000 (0.5 ms).
            var time = CdsTime.Decode(new byte[] { 0x00, 0x01, 0x00, 0x01, 0x2C, 0x80, 0x00 }, UnixEpoch);

            Assert.Equal(1, time.Days);
            Assert.Equal(300u, time.MillisOfDay);
            Assert.Equal(0x8000, time.SubMilli);
            Assert.Equal(86_400_000 + 300, time.ToTimestamp().ToUnixMilliseconds());
        }

        [Fact]
        public void Cds_DecodesFiveOctetField_NoSubMilli()
        {
            var time = CdsTime.Decode(new byte[] { 0x00, 0x01, 0x00, 0x01, 0x2C }, UnixEpoch);

            Assert.Equal(0, time.SubMilli);
            Assert.Equal(86_400_300, time.ToTimestamp().ToUnixMilliseconds());
        }

        [Fact]
        public void Cds_UnsupportedWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CdsTime.Decode(new byte[] { 0x00, 0x01, 0x00 }, UnixEpoch));
        }

        [Fact]
        public void Cds_InvalidMillisOfDay_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CdsTime(UnixEpoch, 0, 86_400_000));
        }
    }
}