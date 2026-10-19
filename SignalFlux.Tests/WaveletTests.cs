using System;
using System.Linq;
using Xunit;
using SignalFlux;
using SignalFlux.SignalProcessing;

namespace SignalFlux.Tests
{
    public class WaveletTests
    {
        private static Signal<double> Sine(double amplitude, double frequencyHz, double sampleRate, int samples)
        {
            var data = new double[samples];
            for (int i = 0; i < samples; i++)
                data[i] = amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            return new Signal<double>(data, sampleRate, Timestamp.UtcNow, unit: null);
        }

        private static double MaxAbsError(double[] a, double[] b)
        {
            double max = 0;
            for (int i = 0; i < a.Length; i++)
                max = Math.Max(max, Math.Abs(a[i] - b[i]));
            return max;
        }

        [Fact]
        public void WaveletTransform_Reconstruct_RoundTripsWithinFloatPrecision()
        {
            var signal = Sine(1.0, 50, 1000, 512);

            var coefficients = signal.WaveletTransform(WaveletKind.Daubechies, taps: 4, levels: 1);
            var reconstructed = coefficients.WaveletReconstruct(
                signal.Frequency, signal.StartTime, signal.Unit, WaveletKind.Daubechies, taps: 4);

            Assert.Equal(signal.Count, reconstructed.Count);
            var error = MaxAbsError(signal.Samples.ToArray(), reconstructed.Samples.ToArray());
            Assert.True(error < 1e-4, $"Wavelet round-trip error was {error}");
        }

        [Fact]
        public void WaveletDenoise_ReducesNoise()
        {
            var rng = new Random(7);
            var data = new double[1024];
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Sin(2 * Math.PI * 50 * i / 1000.0) + (rng.NextDouble() - 0.5);
            var noisy = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var denoised = noisy.WaveletDenoise(threshold: 0.2, soft: true, WaveletKind.Daubechies, taps: 4);

            var dSpan = denoised.Samples.Span;
            double sq = 0;
            for (int i = 0; i < denoised.Count; i++)
            {
                double residual = dSpan[i] - Math.Sin(2 * Math.PI * 50 * i / 1000.0);
                sq += residual * residual;
            }
            double mse = sq / denoised.Count;

            Assert.True(mse < 0.1, $"Wavelet denoise MSE per sample was {mse}; expected tension to fall well below the 1/12 white noise variance.");
        }

        [Fact]
        public void WaveletDenoise_PreservesSignatureShape()
        {
            var rng = new Random(3);
            var data = new double[1024];
            for (int i = 0; i < data.Length; i++)
                data[i] = (i < 512 ? 1.0 : -1.0) + (rng.NextDouble() - 0.5) * 0.2;
            var noisy = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var denoised = noisy.WaveletDenoise(threshold: 0.05, soft: true, WaveletKind.Haar, taps: 1);

            Assert.True(denoised.Samples.Span[256] > 0.5);
            Assert.True(denoised.Samples.Span[768] < -0.5);
        }

        [Fact]
        public void WaveletTransform_TooFewSamples_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 8);
            Assert.Throws<ArgumentException>(() => signal.WaveletTransform());
        }

        [Fact]
        public void WaveletTransform_OddLength_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 1001);
            Assert.Throws<ArgumentException>(() => signal.WaveletTransform());
        }

        [Fact]
        public void WaveletTransform_ExcessiveLevels_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 512);
            Assert.Throws<ArgumentException>(() => signal.WaveletTransform(levels: 1000));
        }

        [Fact]
        public void WaveletDenoise_InvalidThreshold_Throws()
        {
            var signal = Sine(1.0, 50, 1000, 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.WaveletDenoise(threshold: -1));
        }
    }
}