using System;
using System.Linq;
using Xunit;
using SignalFlux;
using SignalFlux.SignalProcessing;

namespace SignalFlux.Tests
{
    public class SignalProcessingTests
    {
        private static Signal<double> Sine(double amplitude, double frequencyHz, double sampleRate, int samples)
        {
            var data = new double[samples];
            for (int i = 0; i < samples; i++)
                data[i] = amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            return new Signal<double>(data, sampleRate, Timestamp.UtcNow, unit: null);
        }

        // ------------------------------------------------------------------
        // FFT / spectrum
        // ------------------------------------------------------------------

        [Fact]
        public void ForwardSpectrum_DominantBinAtExpectedFrequency()
        {
            int n = 1024;
            double sampleRate = 1000;
            double freq = 125; // Integer number of cycles -> no leakage; bin = 128.
            var signal = Sine(1.0, freq, sampleRate, n);

            var spectrum = signal.ForwardSpectrum();
            var axis = signal.FrequencyAxis();

            int expectedBin = (int)(freq * n / sampleRate);
            var magnitudes = spectrum.Select(c => c.Magnitude).ToArray();
            double dominant = magnitudes[expectedBin];

            Assert.True(dominant > 10 * magnitudes[expectedBin + 1]);
            Assert.True(dominant > 10 * magnitudes[expectedBin - 1]);
            Assert.Equal(freq, axis[expectedBin], precision: 6);
        }

        [Fact]
        public void InverseSpectrum_RoundTripsOriginalSignal()
        {
            int n = 256;
            double sampleRate = 1000;
            var signal = Sine(2.0, 50, sampleRate, n);

            var spectrum = signal.ForwardSpectrum();
            var reconstructed = spectrum.InverseSpectrum(sampleRate, signal.StartTime, signal.Unit);

            Assert.Equal(signal.Count, reconstructed.Count);
            Assert.Equal(signal.Frequency, reconstructed.Frequency);
            Assert.Equal(signal.Unit, reconstructed.Unit);

            var orig = signal.Samples.Span;
            var rec = reconstructed.Samples.Span;
            for (int i = 0; i < orig.Length; i++)
                Assert.Equal(orig[i], rec[i], precision: 6);
        }

        [Fact]
        public void PowerSpectrum_LengthMatchesSampleCount()
        {
            var signal = Sine(1.0, 100, 1000, 128);
            Assert.Equal(128, signal.PowerSpectrum().Length);
        }

        [Fact]
        public void FrequencyAxis_StartsAtZero()
        {
            var signal = Sine(1.0, 100, 8000, 64);
            var axis = signal.FrequencyAxis();
            Assert.Equal(0.0, axis[0], precision: 12);
            Assert.Equal(8000.0 / 64, axis[1], precision: 12);
        }

        [Fact]
        public void ForwardSpectrum_EmptySignal_ReturnsEmpty()
        {
            var signal = new Signal<double>(Array.Empty<double>(), 1000, Timestamp.UtcNow);
            Assert.Empty(signal.ForwardSpectrum());
            Assert.Empty(signal.PowerSpectralDensity());
        }

        // ------------------------------------------------------------------
        // Peak detection
        // ------------------------------------------------------------------

        [Fact]
        public void FindPeaks_DetectsWaveformPeaks()
        {
            int n = 800;
            double sampleRate = 800; // 4 full cycles over 800 samples -> exactly 4 peaks.
            var signal = Sine(1.0, 4, sampleRate, n);

            var peaks = signal.FindPeaks(threshold: 0.5);
            Assert.Equal(4, peaks.Length);

            foreach (var idx in peaks)
                Assert.True(signal.Samples.Span[idx] > 0.9);
        }

        [Fact]
        public void FindPeaksDetailed_ReturnsIndexAndValue()
        {
            var signal = Sine(1.0, 2, 800, 800);
            var peaks = signal.FindPeaksDetailed(threshold: 0.5);

            Assert.Equal(2, peaks.Length);
            foreach (var p in peaks)
            {
                Assert.True(p.Index >= 0 && p.Index < signal.Count);
                Assert.True(p.Value > 0.9);
            }
        }

        [Fact]
        public void FindPeaks_MinimumDistanceReducesCount()
        {
            var data = new double[] { 0.1, 0.9, 0.2, 0.8, 0.3 };
            var signal = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var noDist = signal.FindPeaks(threshold: 0.5);
            var withDist = signal.FindPeaks(threshold: 0.5, minDistance: 3);

            Assert.True(withDist.Length < noDist.Length);
        }

        [Fact]
        public void FindPeaks_EmptySignal_ReturnsEmpty()
        {
            var signal = new Signal<double>(Array.Empty<double>(), 1000, Timestamp.UtcNow);
            Assert.Empty(signal.FindPeaks());
        }

        // ------------------------------------------------------------------
        // PSD
        // ------------------------------------------------------------------

        [Fact]
        public void PowerSpectralDensity_OneSidedLength()
        {
            int n = 1024;
            var signal = Sine(1.0, 100, 1000, n);

            var psd = signal.PowerSpectralDensity();
            Assert.Equal(n / 2 + 1, psd.Length);
            Assert.True(psd.All(p => p >= 0));
        }

        // ------------------------------------------------------------------
        // Envelope
        // ------------------------------------------------------------------

        [Fact]
        public void Envelope_TracksMovingMax()
        {
            var data = new double[] { 1.0, 2.0, 3.0, 2.0, 1.0, 0.0 };
            var signal = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var env = signal.Envelope(3);

            // Window size 3 -> radius 1. At index 2 (value 3) window [2,3,2] max 3.
            Assert.Equal(3.0, env.Samples.Span[2], precision: 12);
            Assert.Equal(3.0, env.Samples.Span[1], precision: 12);
        }

        [Fact]
        public void Envelope_InvalidWindow_Throws()
        {
            var signal = Sine(1.0, 100, 1000, 64);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.Envelope(0));
        }
    }
}
