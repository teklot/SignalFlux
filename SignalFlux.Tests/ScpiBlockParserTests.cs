using System.Text;
using SignalFlux.Protocols.Scpi;

namespace SignalFlux.Tests
{
    public class ScpiBlockParserTests
    {
        [Fact]
        public void Parse_DefiniteLength_ReadsPayload()
        {
            var raw = Encoding.ASCII.GetBytes("#14ABCD");
            ScpiBlock block = ScpiBlockParser.Parse(raw);

            Assert.Equal(4, block.Length);
            Assert.Equal(new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D' }, block.Data);
        }

        [Fact]
        public void Parse_TrailingTerminator_IsIgnored()
        {
            var raw = Encoding.ASCII.GetBytes("#13abc\n");
            ScpiBlock block = ScpiBlockParser.Parse(raw);

            Assert.Equal(3, block.Length);
            Assert.Equal(new byte[] { (byte)'a', (byte)'b', (byte)'c' }, block.Data);
        }

        [Fact]
        public void Parse_IndefiniteLength_ReadsUntilEnd()
        {
            var raw = Encoding.ASCII.GetBytes("#0xyz");
            ScpiBlock block = ScpiBlockParser.Parse(raw);

            Assert.Equal(3, block.Length);
            Assert.Equal(new byte[] { (byte)'x', (byte)'y', (byte)'z' }, block.Data);
        }

        [Fact]
        public void Parse_MissingHash_Throws()
        {
            var raw = Encoding.ASCII.GetBytes("41234");

            Assert.Throws<System.FormatException>(() => ScpiBlockParser.Parse(raw));
        }

        [Fact]
        public void Parse_LongLengthField_ReadsPayload()
        {
            var payload = new byte[600]; // length 600 needs 3 digits -> #3600...
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);
            var raw = new byte[5 + payload.Length];
            raw[0] = (byte)'#';
            raw[1] = (byte)'3';
            raw[2] = (byte)'6';
            raw[3] = (byte)'0';
            raw[4] = (byte)'0';
            System.Array.Copy(payload, 0, raw, 5, payload.Length);

            ScpiBlock block = ScpiBlockParser.Parse(raw);

            Assert.Equal(payload, block.Data);
        }

        [Fact]
        public void Parse_TruncatedPayload_Throws()
        {
            var raw = Encoding.ASCII.GetBytes("#41234A");

            Assert.Throws<System.FormatException>(() => ScpiBlockParser.Parse(raw));
        }

        [Fact]
        public void ParseFromReader_StreamsBytes()
        {
            var response = Encoding.ASCII.GetBytes("#14abcd");
            int index = 0;

            ScpiBlock block = ScpiBlockParser.ParseFromReader(() => index < response.Length ? response[index++] : -1);

            Assert.Equal(4, block.Length);
            Assert.Equal(new byte[] { (byte)'a', (byte)'b', (byte)'c', (byte)'d' }, block.Data);
        }

        [Fact]
        public void ParseFromReader_TruncatedAtEof_Throws()
        {
            var response = Encoding.ASCII.GetBytes("#4");
            int index = 0;

            Assert.Throws<System.FormatException>(() =>
                ScpiBlockParser.ParseFromReader(() => index < response.Length ? response[index++] : -1));
        }

        [Fact]
        public void Encode_RoundTripsDoubles()
        {
            double[] samples = { 0.0, 1.0, -1.0, 3.14159, double.Epsilon };
            byte[] encoded = ScpiBlockParser.Encode(samples);

            ScpiBlock block = ScpiBlockParser.Parse(encoded);

            Assert.Equal(samples, block.ToDoubles());
        }

        [Fact]
        public void ToDoubles_NonMultipleOf8_Throws()
        {
            var block = new ScpiBlock(new byte[] { 1, 2, 3, 4 });

            Assert.Throws<System.InvalidOperationException>(() => block.ToDoubles());
        }
    }
}