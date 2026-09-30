using System.Text;
using System.Threading.Tasks;
using SignalFlux.Protocols.Scpi;
using UnitsNet.Units;

namespace SignalFlux.Tests
{
    public class OscilloscopeWaveformSignalTests
    {
        [Fact]
        public async Task QueryWaveformAsync_BuildsSignalFromQueries()
        {
            double[] samples = { 1.0, 2.0, -1.5, 0.25 };
            byte[] encoded = ScpiBlockParser.Encode(samples);
            var connection = new InMemoryScpiConnection(cmd =>
            {
                if (cmd == ":WAVeform:DATA?") return encoded;
                if (cmd == ":WAVeform:XINC?") return Encoding.ASCII.GetBytes("1.25E-05\n");
                return null;
            });
            var client = new ScpiClient(connection);

            var signal = await client.QueryWaveformAsync(channel: "CH2", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(80000.0, signal.Frequency, 6); // 1 / 1.25E-05
            Assert.Equal(4, signal.Samples.Length);
            Assert.Equal(ElectricPotentialUnit.Volt, signal.Unit);
            Assert.Equal("scpi:CH2", signal.Source);
            Assert.Equal(samples, signal.Samples.ToArray());
            Assert.Equal(Quality.Good, signal.Quality);
        }

        [Fact]
        public async Task QueryWaveformAsync_InvalidXinc_Throws()
        {
            var connection = new InMemoryScpiConnection(cmd =>
            {
                if (cmd == ":WAVeform:DATA?") return ScpiBlockParser.Encode(new double[] { 1.0 });
                if (cmd == ":WAVeform:XINC?") return Encoding.ASCII.GetBytes("nonsense\n");
                return null;
            });
            var client = new ScpiClient(connection);

            await Assert.ThrowsAsync<System.FormatException>(
                () => client.QueryWaveformAsync(cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public void ToSignal_AppliesGainAndOffset()
        {
            var block = ScpiBlockParser.Parse(ScpiBlockParser.Encode(new double[] { 1.0, 2.0, 3.0 }));

            var signal = block.ToSignal(
                100.0,
                SignalFlux.Timestamp.Now,
                gain: 10.0,
                offset: 5.0,
                unit: ElectricPotentialUnit.Volt,
                source: "scope");

            Assert.Equal(new double[] { 15.0, 25.0, 35.0 }, signal.Samples.ToArray());
            Assert.Equal(100.0, signal.Frequency, 6);
            Assert.Equal(ElectricPotentialUnit.Volt, signal.Unit);
            Assert.Equal("scope", signal.Source);
        }

        [Fact]
        public void ToSignal_EmptyBlock_ProducesEmptySignal()
        {
            var empty = new ScpiBlock(System.Array.Empty<byte>());

            var signal = empty.ToSignal(50.0, SignalFlux.Timestamp.Now, source: "scope");

            Assert.Equal(0, signal.Samples.Length);
            Assert.Equal(50.0, signal.Frequency, 6);
        }
    }
}