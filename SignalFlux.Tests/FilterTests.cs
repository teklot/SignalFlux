using System;
using System.Linq;
using Xunit;
using SignalFlux;
using SignalFlux.SignalProcessing;

namespace SignalFlux.Tests
{
    public class FilterTests
    {
        private static Signal<double> Sine(double amplitude, double frequencyHz, double sampleRate, int samples)
        {
            var data = new double[samples];
            for (int i = 0; i < samples; i++)
                data[i] = amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            return new Signal<double>(data, sampleRate, Timestamp.UtcNow, unit: null);
        }

        private static double SteadyStateAmplitude(Signal<double> signal)
        {
            // Ignore the IIR transient: measure the max magnitude over the second half.
            var samples = signal.Samples.Span;
            double max = 0;
            for (int i = samples.Length / 2; i < samples.Length; i++)
                max = Math.Max(max, Math.Abs(samples[i]));
            return max;
        }

        [Fact]
        public void LowPass_KeepsInBandTone()
        {
            var signal = Sine(1.0, 50, 1000, 2048);
            var filtered = signal.LowPass(cutoffFrequencyHz: 80, order: 4);

            Assert.Equal(signal.Count, filtered.Count);
            Assert.True(SteadyStateAmplitude(filtered) > 0.9, "In-band tone amplitude should be preserved.");
        }

        [Fact]
        public void LowPass_AttenuatesOutOfBandTone()
        {
            var signal = Sine(1.0, 400, 1000, 2048);
            var filtered = signal.LowPass(cutoffFrequencyHz: 80, order: 4);

            Assert.True(SteadyStateAmplitude(filtered) < 0.05, "Out-of-band tone should be suppressed by at least 26 dB.");
        }

        [Fact]
        public void HighPass_RejectsLowFrequency()
        {
            var signal = Sine(1.0, 10, 1000, 2048);
            var filtered = signal.HighPass(cutoffFrequencyHz: 100, order: 4);
            Assert.True(SteadyStateAmplitude(filtered) < 0.05);
        }

        [Fact]
        public void HighPass_KeepsHighFrequency()
        {
            var signal = Sine(1.0, 300, 1000, 2048);
            var filtered = signal.HighPass(cutoffFrequencyHz: 100, order: 4);
            Assert.True(SteadyStateAmplitude(filtered) > 0.9);
        }

        [Fact]
        public void BandPass_KeepsInBandAndRejectsSides()
        {
            var inBand = Sine(1.0, 200, 1000, 2048).BandPass(150, 250, 4);
            var outBand = Sine(1.0, 70, 1000, 2048).BandPass(150, 250, 4);

            Assert.True(SteadyStateAmplitude(inBand) > 0.8);
            Assert.True(SteadyStateAmplitude(outBand) < 0.1);
        }

        [Fact]
        public void BandStop_RejectsInBandAndKeepsSides()
        {
            var inBand = Sine(1.0, 130, 1000, 2048).BandStop(100, 150, 4);
            var lowSide = Sine(1.0, 50, 1000, 2048).BandStop(100, 150, 4);
            var highSide = Sine(1.0, 200, 1000, 2048).BandStop(100, 150, 4);

            Assert.True(SteadyStateAmplitude(inBand) < 0.1);
            Assert.True(SteadyStateAmplitude(lowSide) > 0.8);
            Assert.True(SteadyStateAmplitude(highSide) > 0.8);
        }

        [Fact]
        public void Denoise_ReducesNoiseVariance()
        {
            var rng = new Random(42);
            var data = new double[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Sin(2 * Math.PI * 50 * i / 1000.0) + (rng.NextDouble() - 0.5) * 0.5;
            var noisy = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var denoised = noisy.Denoise(windowSize: 15, derivative: 0);
            var residual = new double[denoised.Count];
            var dSpan = denoised.Samples.Span;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < residual.Length; i++)
            {
                residual[i] = dSpan[i] - Math.Sin(2 * Math.PI * 50 * i / 1000.0);
                sum += residual[i];
                sumSq += residual[i] * residual[i];
            }
            double variance = sumSq / residual.Length - (sum / residual.Length) * (sum / residual.Length);

            Assert.True(variance < 0.01, $"Smoothed residual variance should be well below the ~0.02 raw noise variance, was {variance}");
        }

        [Fact]
        public void Denoise_PreservesMetadata()
        {
            var signal = Sine(1.0, 50, 1000, 1024);
            var denoised = signal.Denoise();

            Assert.Equal(signal.Frequency, denoised.Frequency);
            Assert.Equal(signal.StartTime, denoised.StartTime);
            Assert.Equal(signal.Unit, denoised.Unit);
            Assert.Equal(signal.Count, denoised.Count);
        }

        [Fact]
        public void LowPass_InvalidCutoff_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 1024);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.LowPass(cutoffFrequencyHz: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.LowPass(cutoffFrequencyHz: 600)); // above Nyquist
        }

        [Fact]
        public void Filters_InvalidOrder_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 1024);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.LowPass(80, order: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.BandPass(150, 250, order: -1));
        }

        [Fact]
        public void BandPass_CrossedEdges_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 1024);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.BandPass(250, 150));
        }

        [Fact]
        public void Denoise_InvalidWindow_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 1024);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.Denoise(windowSize: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.Denoise(windowSize: 33));
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.Denoise(derivative: 3));
        }

        [Fact]
        public void Filter_EmptySignal_ReturnsEmpty()
        {
            var signal = new Signal<double>(Array.Empty<double>(), 1000, Timestamp.UtcNow);
            Assert.Equal(0, signal.LowPass(80).Count);
            Assert.Equal(0, signal.Denoise().Count);
        }
    }
}