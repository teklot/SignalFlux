using Xunit;
using SignalFlux.Protocols.Arinc429;

namespace SignalFlux.Tests
{
    public class Arinc429Tests
    {
        // Label 0o203 transmitted with bits laid out per ARINC bit numbering.
        private const uint RawWord = 0x83_00_14_05u;

        [Fact]
        public void Constructor_Fields_AssembleWord()
        {
            var word = new Arinc429Word(
                label: 0x83,
                sdi: 0b01,
                data: 0x2_80,   // 19 bits with SDI/SSM untouched afterward
                ssm: 0b00,
                parity: 1);

            Assert.Equal(0x83u, word.Label);
            Assert.Equal(0b01, word.Sdi);
            Assert.Equal(0x2_80u, word.Data);
            Assert.Equal(0b00, word.Ssm);
            Assert.Equal(1u, word.Parity);
        }

        [Fact]
        public void Constructor_FromRaw_CorrectFieldPositions()
        {
            var word = new Arinc429Word(RawWord);
            Assert.Equal(0x83u, word.Label);
        }

        [Fact]
        public void Data_ExceedsNineteenBits_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new Arinc429Word(0x83, 0, 0x8_0000, 0, 0));
        }

        [Fact]
        public void HasOddParity_ComputesOverWholeWord()
        {
            // 0x00000001 has a single set bit -> odd parity.
            Assert.True(new Arinc429Word(0x00000001u).HasOddParity);

            // 0x00000000 has no set bits -> even parity.
            Assert.False(new Arinc429Word(0x00000000u).HasOddParity);
        }

        [Fact]
        public void WithOddParity_SetsParityBit()
        {
            var word = new Arinc429Word(0x00000000u);
            var fixedWord = word.WithOddParity();
            Assert.True(fixedWord.HasOddParity);
            Assert.Equal(0x00000001u, fixedWord.Word);
        }

        [Fact]
        public void WithEvenParity_ClearsParityBit()
        {
            var word = new Arinc429Word(0x00000001u);
            var fixedWord = word.WithEvenParity();
            Assert.False(fixedWord.HasOddParity);
            Assert.Equal(0x00000000u, fixedWord.Word);
        }

        [Fact]
        public void Equals_ByRawWord()
        {
            var a = new Arinc429Word(0x12345678u);
            var b = new Arinc429Word(0x12345678u);
            Assert.Equal(a, b);
        }

        [Fact]
        public void DecodeBnr_AppliesSignAndWeight()
        {
            // Data field 0 with a positive sign bit: value 0.
            var word = new Arinc429Word(0x83, 0, 0x0, 0, 0);
            Assert.Equal(0.0, word.DecodeBnr(), 6);

            // Data field = 4 (bit 2 of data), LSB weight 0.5 -> 2.0.
            var word2 = new Arinc429Word(0x83, 0, 0x4, 0, 0);
            Assert.Equal(2.0, word2.DecodeBnr(0.5), 6);
        }

        [Fact]
        public void DecodeBnr_Negative_TwosComplement()
        {
            // 19-bit two's complement: all ones = -1.
            var word = new Arinc429Word(0x83, 0, 0x7FFFF, 0, 0);
            Assert.Equal(-1.0, word.DecodeBnr(), 6);
        }

        [Fact]
        public void EncodeBnr_RoundTrips()
        {
            var baseWord = new Arinc429Word(0x83, 0, 0x0, 1, 0);
            var encoded = baseWord.EncodeBnr(42.0, lsbWeight: 2.0);
            Assert.Equal(42.0, encoded.DecodeBnr(2.0), 6);
            Assert.Equal(0x83u, encoded.Label);
            Assert.Equal(1u, encoded.Ssm);
        }

        [Fact]
        public void ToBnrMeasurement_MapsQualityBySsm()
        {
            var good = new Arinc429Word(0x83, 0, 0x28, 0, 0);
            var m = good.ToBnrMeasurement(lsbWeight: 0.5);
            Assert.Equal(Quality.Good, m.Quality);
            Assert.Equal(20.0, m.Value, 6);

            var bad = new Arinc429Word(0x83, 0, 0x28, 3, 0);
            Assert.Equal(Quality.Bad, bad.ToBnrMeasurement().Quality);
        }

        [Fact]
        public void ToString_FormatsHex()
        {
            var word = new Arinc429Word(0x1234ABCDu);
            Assert.Contains("1234ABCD", word.ToString());
        }
    }
}