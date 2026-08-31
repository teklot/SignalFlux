using System;
using Xunit;
using SignalFlux.Protocols.Can;

namespace SignalFlux.Tests
{
    public class CanFrameTests
    {
        [Fact]
        public void Constructor_StandardIdAndPayload_StoresValues()
        {
            byte[] payload = { 0xAA, 0xBB, 0xCC };
            var frame = new CanFrame(0x123, payload, Timestamp.Zero);

            Assert.Equal(0x123u, frame.Id);
            Assert.Equal(3, frame.DataLength);
            Assert.Equal(0xAA, frame[0]);
            Assert.Equal(0xBB, frame[1]);
            Assert.Equal(0xCC, frame[2]);
            Assert.Equal(Timestamp.Zero, frame.Timestamp);
            Assert.False(frame.IsExtended);
            Assert.False(frame.IsRemoteRequest);
            Assert.True(frame.IsValid);
        }

        [Fact]
        public void Constructor_DefaultTimestamp_UsesNow()
        {
            var before = Timestamp.Now;
            var frame = new CanFrame(0x100, new byte[] { 0x00 });
            var after = Timestamp.Now;

            Assert.InRange(frame.Timestamp.Ticks, before.Ticks, after.Ticks);
        }

        [Fact]
        public void Constructor_IdAboveStandardLimit_IsMasked()
        {
            var frame = new CanFrame(0xABC, new byte[] { });
            Assert.True(frame.Id <= CanFrame.MaxStandardId);
        }

        [Fact]
        public void Constructor_PayloadOverEightBytes_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CanFrame(0x123, new byte[9]));
        }

        [Fact]
        public void Equals_SameIdAndPayload_ReturnsTrue()
        {
            var a = new CanFrame(0x200, new byte[] { 1, 2, 3 });
            var b = new CanFrame(0x200, new byte[] { 1, 2, 3 });

            Assert.True(a == b);
            Assert.Equal(a, b);
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentId_ReturnsFalse()
        {
            var a = new CanFrame(0x200, new byte[] { 1, 2, 3 });
            var b = new CanFrame(0x201, new byte[] { 1, 2, 3 });

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void RemoteRequest_RequiresEmptyPayload()
        {
            var frame = new CanFrame(0x123, Array.Empty<byte>(), Timestamp.Zero, isRemoteRequest: true);
            Assert.True(frame.IsRemoteRequest);
            Assert.True(frame.IsValid);
        }

        [Fact]
        public void ToString_FormatsIdAndLength()
        {
            var frame = new CanFrame(0x123, new byte[] { 0x01, 0x02 }, Timestamp.Zero);
            string s = frame.ToString();

            Assert.Contains("123", s);
            Assert.Contains("2", s);
        }
    }
}