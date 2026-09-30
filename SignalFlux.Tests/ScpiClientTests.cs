using System.Text;
using System.Threading.Tasks;
using SignalFlux.Protocols.Scpi;

namespace SignalFlux.Tests
{
    public class ScpiClientTests
    {
        [Fact]
        public async Task IdnAsync_ReturnsIdentification()
        {
            var connection = new InMemoryScpiConnection(cmd =>
                cmd == "*IDN?" ? Encoding.ASCII.GetBytes("TekLot,SCPI-Simulator,SN-0001,1.0.0\n") : null);
            var client = new ScpiClient(connection);

            string idn = await client.IdnAsync(TestContext.Current.CancellationToken);

            Assert.Equal("TekLot,SCPI-Simulator,SN-0001,1.0.0", idn);
            Assert.Equal(SignalFlux.IO.ConnectionState.Connected, connection.State);
        }

        [Fact]
        public async Task QueryAsync_TrimsTerminatorAndCarriageReturn()
        {
            var connection = new InMemoryScpiConnection(cmd =>
                cmd == "MEASure:VOLT:DC?" ? Encoding.ASCII.GetBytes("4.950000E+00\r\n") : null);
            var client = new ScpiClient(connection, "\r\n");

            string response = await client.QueryAsync("MEASure:VOLT:DC?", TestContext.Current.CancellationToken);

            Assert.Equal("4.950000E+00", response);
        }

        [Fact]
        public async Task WriteAsync_CommandDoesNotExpectResponse()
        {
            string? received = null;
            var connection = new InMemoryScpiConnection(cmd =>
            {
                received = cmd;
                return null;
            });
            var client = new ScpiClient(connection);

            await client.WriteAsync(":WAVeform:DATA?", TestContext.Current.CancellationToken);
            // No response expected; command still reached the instrument.
            Assert.Equal(":WAVeform:DATA?", received);
        }

        [Fact]
        public async Task QueryBlockAsync_ReadsDefiniteLengthBlock()
        {
            double[] samples = { 1.0, -2.5, 3.25 };
            byte[] encoded = ScpiBlockParser.Encode(samples);
            var connection = new InMemoryScpiConnection(cmd =>
                cmd == ":WAVeform:DATA?" ? encoded : null);
            var client = new ScpiClient(connection);

            ScpiBlock block = await client.QueryBlockAsync(":WAVeform:DATA?", TestContext.Current.CancellationToken);

            Assert.Equal(samples, block.ToDoubles());
        }

        [Fact]
        public async Task QueryBlockAsync_ReadsIndefiniteLengthBlock()
        {
            double[] samples = { 0.5, -0.25 };
            byte[] encoded = ScpiBlockParser.Encode(samples);
            int lengthDigits = encoded[1] - '0';
            int headerSize = 2 + lengthDigits;
            // Rebuild as an indefinite-length block: #0 + payload + line terminator.
            var raw = new byte[3 + (encoded.Length - headerSize)];
            raw[0] = (byte)'#';
            raw[1] = (byte)'0';
            System.Array.Copy(encoded, headerSize, raw, 2, encoded.Length - headerSize);
            raw[raw.Length - 1] = (byte)'\n';
            var connection = new InMemoryScpiConnection(cmd => cmd == ":WAVeform:DATA?" ? raw : null);
            var client = new ScpiClient(connection);

            ScpiBlock block = await client.QueryBlockAsync(":WAVeform:DATA?", TestContext.Current.CancellationToken);

            Assert.Equal(samples, block.ToDoubles());
        }

        [Fact]
        public async Task ReadLineAsync_HandlesFragmentedWrites()
        {
            var connection = new InMemoryScpiConnection(cmd =>
                cmd == "*IDN?" ? Encoding.ASCII.GetBytes("A,B,C,D\n") : null);
            var client = new ScpiClient(connection);
            await client.EnsureConnectedAsync(TestContext.Current.CancellationToken);

            // Fragment the bytes of the line across writes; the connection still assembles the command.
            await connection.WriteAsync(Encoding.ASCII.GetBytes("*ID"), TestContext.Current.CancellationToken);
            await connection.WriteAsync(Encoding.ASCII.GetBytes("N?\n"), TestContext.Current.CancellationToken);

            string response = await client.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.Equal("A,B,C,D", response);
        }

        [Fact]
        public async Task QueryAsync_WithNoResponder_BlocksUntilCancelled()
        {
            var connection = new InMemoryScpiConnection((string _) => null);
            var client = new ScpiClient(connection);

            await client.WriteAsync("*IDN?", TestContext.Current.CancellationToken);
            using (var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromMilliseconds(200)))
            {
                await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.ReadLineAsync(cts.Token));
            }
        }

        [Fact]
        public async Task ReadBlockAsync_MalformedHeader_Throws()
        {
            var connection = new InMemoryScpiConnection(cmd =>
                cmd == ":WAVeform:DATA?" ? Encoding.ASCII.GetBytes("XYZ") : null);
            var client = new ScpiClient(connection);

            await Assert.ThrowsAsync<ScpiProtocolException>(
                () => client.QueryBlockAsync(":WAVeform:DATA?", TestContext.Current.CancellationToken));
        }
    }
}