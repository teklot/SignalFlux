using System;
using Xunit;
using SignalFlux.Protocols.Can;

namespace SignalFlux.Tests
{
    public class CanSignalTests
    {
        // ------------------------------------------------------------------
        // Intel (little-endian) decoding
        // ------------------------------------------------------------------

        [Fact]
        public void GetRawValue_Intel16Bit_ReadsLittleEndian()
        {
            // startBit=0, 16-bit -> LSB in byte 0, MSB in byte 1.
            var frame = new CanFrame(0x100, new byte[] { 0x34, 0x12, 0x00, 0x00 });

            ulong value = frame.GetRawValue(0, 16, CanByteOrder.LittleEndian);
            Assert.Equal(0x1234UL, value);
        }

        [Fact]
        public void GetRawValue_IntelSingleBit_ReadsStartBit()
        {
            var frame = new CanFrame(0x100, new byte[] { 0x04, 0x00 });
            ulong value = frame.GetRawValue(2, 1, CanByteOrder.LittleEndian);
            Assert.Equal(1UL, value);
        }

        // ------------------------------------------------------------------
        // Motorola (big-endian) decoding
        // ------------------------------------------------------------------

        [Fact]
        public void GetRawValue_Motorola16Bit_ReadsBigEndian()
        {
            // A Motorola 16-bit signal with MSB at bit 7 of byte 0 reads big-endian:
            // byte0 is the high byte, byte1 the low byte.
            var frame = new CanFrame(0x100, new byte[] { 0x12, 0x34, 0x00, 0x00 });
            ulong value = frame.GetRawValue(7, 16, CanByteOrder.BigEndian);
            Assert.Equal(0x1234UL, value);
        }

        [Fact]
        public void GetRawValue_Motorola16Bit_AcrossByteBoundary_ReadsSawtooth()
        {
            // start bit 11 (byte 1, bit 3), length 12: walks byte1 bits 3..0, then byte2 bits 7..0.
            // MSB-first value: (byte1 bits 3..0) followed by byte2.
            var frame = new CanFrame(0x100, new byte[] { 0x00, 0x0A, 0x5C, 0x00 });
            ulong value = frame.GetRawValue(11, 12, CanByteOrder.BigEndian);
            // byte1 bits 3..0 = 0xA = 1010 (high nibble of value), byte2 = 0x5C -> 0xA5C
            Assert.Equal(0xA5CUL, value);
        }

        // ------------------------------------------------------------------
        // Signed decoding
        // ------------------------------------------------------------------

        [Fact]
        public void GetRawSignedValue_NegativeIntel_ReturnsTwosComplement()
        {
            // Two's complement -2 in 8 bits = 0xFE.
            var frame = new CanFrame(0x100, new byte[] { 0xFE, 0x00 });
            long value = frame.GetRawSignedValue(0, 8, CanByteOrder.LittleEndian);
            Assert.Equal(-2L, value);
        }

        [Fact]
        public void GetRawSignedValue_PositiveIntel_Unaltered()
        {
            var frame = new CanFrame(0x100, new byte[] { 0x7F, 0x00 });
            long value = frame.GetRawSignedValue(0, 8, CanByteOrder.LittleEndian);
            Assert.Equal(127L, value);
        }

        // ------------------------------------------------------------------
        // Encoding (round-trip)
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(CanByteOrder.LittleEndian, 0, 16, 0x1234UL)]
        [InlineData(CanByteOrder.BigEndian, 7, 16, 0x1234UL)]
        [InlineData(CanByteOrder.LittleEndian, 3, 12, 0xABCUL)]
        [InlineData(CanByteOrder.BigEndian, 7, 8, 0x5AUL)]
        [InlineData(CanByteOrder.BigEndian, 11, 12, 0xA5CUL)]
        [InlineData(CanByteOrder.BigEndian, 3, 20, 0x1FEDCUL)]
        public void EncodeRawValue_RoundTrips(CanByteOrder order, int startBit, int length, ulong raw)
        {
            byte[] payload = CanSignalExtensions.EncodeRawValue(raw, startBit, length, order);
            var frame = new CanFrame(0x100, payload);
            Assert.Equal(raw, frame.GetRawValue(startBit, length, order));
        }

        // ------------------------------------------------------------------
        // Physical conversion
        // ------------------------------------------------------------------

        [Fact]
        public void ToPhysicalValue_AppliesFactorAndOffset()
        {
            var frame = new CanFrame(0x100, new byte[] { 0x0A, 0x00 });
            double value = frame.ToPhysicalValue(0, 8, factor: 0.5, offset: -1.0);
            Assert.Equal(4.0, value, 6);
        }

        [Fact]
        public void EncodePhysicalValue_RoundTrips()
        {
            byte[] payload = CanSignalExtensions.EncodePhysicalValue(25.0, 0, 8, factor: 0.1, offset: 0);
            var frame = new CanFrame(0x100, payload);
            Assert.Equal(25.0, frame.ToPhysicalValue(0, 8, factor: 0.1, offset: 0), 6);
        }

        [Fact]
        public void ToMeasurement_MapsQualityAndMetadata()
        {
            var frame = new CanFrame(0x123, new byte[] { 0x0A }, Timestamp.Zero);
            var m = frame.ToMeasurement(startBit: 0, length: 8, factor: 1.0, offset: 0);
            Assert.Equal(10.0, m.Value, 6);
            Assert.Equal(Quality.Good, m.Quality);
            Assert.Contains(m.Metadata.Keys, k => k == "id");
            Assert.Contains(m.Metadata.Keys, k => k == "source");
        }

        [Fact]
        public void ToMeasurement_OutOfRange_QualityBad()
        {
            var frame = new CanFrame(0x123, new byte[] { 0x64 }, Timestamp.Zero);
            var m = frame.ToMeasurement(startBit: 0, length: 8, physicalMinimum: 0, physicalMaximum: 10);
            Assert.Equal(100.0, m.Value, 6);
            Assert.Equal(Quality.Bad, m.Quality);
        }

        [Fact]
        public void GetRawValue_SignalBeyondPayload_Throws()
        {
            var frame = new CanFrame(0x100, new byte[] { 0x01 });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                frame.GetRawValue(0, 16, CanByteOrder.LittleEndian));
        }
    }
}