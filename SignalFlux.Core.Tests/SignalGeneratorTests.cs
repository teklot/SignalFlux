using Xunit;
using SignalFlux.Generators;

namespace SignalFlux.Tests
{
    public class SignalGeneratorTests
    {
        [Fact]
        public void SineGenerator_GeneratesCorrectCount()
        {
            var gen = new SineGenerator(frequency: 100);
            var signal = gen.GenerateSignal(1000);

            Assert.Equal(1000, signal.Count);
            Assert.Equal(100, signal.Frequency);
        }

        [Fact]
        public void SineGenerator_FirstSampleIsZero()
        {
            var gen = new SineGenerator(frequency: 1, amplitude: 1.0);
            var signal = gen.GenerateSignal(100);

            var samples = signal.Samples.Span;
            Assert.Equal(0.0, samples[0], 4);
        }

        [Fact]
        public void SquareGenerator_ProducesCorrectAmplitude()
        {
            var gen = new SquareGenerator(frequency: 10, amplitude: 5.0);
            var signal = gen.GenerateSignal(100);

            var samples = signal.Samples.Span;
            Assert.Equal(5.0, samples[0]);
            Assert.Equal(-5.0, samples[75]);
        }

        [Fact]
        public void NoiseGenerator_ProducesVaryingValues()
        {
            var gen = new NoiseGenerator(frequency: 100, amplitude: 1.0, seed: 42);
            var signal = gen.GenerateSignal(100);

            bool allSame = true;
            var samples = signal.Samples.Span;
            for (int i = 1; i < samples.Length; i++)
                if (Math.Abs(samples[i] - samples[0]) > 0.001) { allSame = false; break; }

            Assert.False(allSame, "Noise samples should vary");
        }

        [Fact]
        public void RampGenerator_ProducesLinearRamp()
        {
            var gen = new RampGenerator(frequency: 100, slope: 2.0);
            var signal = gen.GenerateSignal(100);

            var samples = signal.Samples.Span;
            Assert.Equal(0.0, samples[0], 4);
            Assert.Equal(0.02, samples[1], 4);
        }

        [Fact]
        public void SawtoothGenerator_ProducesCorrectShape()
        {
            var gen = new SawtoothGenerator(frequency: 10, amplitude: 2.0);
            var signal = gen.GenerateSignal(100);

            var samples = signal.Samples.Span;
            Assert.Equal(-2.0, samples[0], 4);
            Assert.Equal(2.0, samples[10], 1);
        }

        [Fact]
        public void RandomWalkGenerator_ProducesVaryingValues()
        {
            var gen = new RandomWalkGenerator(frequency: 100, stepSize: 0.1, seed: 42);
            var signal = gen.GenerateSignal(100);

            Assert.Equal(100, signal.Count);
        }
    }
}
