using System;
using System.Linq;
using Xunit;
using SignalFlux;
using SignalFlux.SignalProcessing;
using NWaves.Windows;

namespace SignalFlux.Tests
{
    public class WelchPsdTests
    {
        private static Signal<double> Sine(double amplitude, double frequencyHz, double sampleRate, int samples)
        {
            var data = new double[samples];
            for (int i = 0; i < samples; i++)
                data[i] = amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            return new Signal<double>(data, sampleRate, Timestamp.UtcNow, unit: null);
        }

        [Fact]
        public void WelchPsd_PeaksNearInjectedTone()
        {
            var signal = Sine(1.0, 100, 1000, 2048);
            var psd = signal.WelchPsd(segmentSize: 256);

            Assert.Equal(psd.Frequencies.Length, psd.Power.Length);
            Assert.True(psd.Power.All(p => p >= 0), "PSD must be non-negative.");

            int peak = 0;
            for (int i = 1; i < psd.Power.Length; i++)
                if (psd.Power[i] > psd.Power[peak])
                    peak = i;

            double resolution = 1000.0 / 256; // 3.9 Hz per bin
            Assert.InRange(psd.Frequencies[peak], 100 - resolution, 100 + resolution);
        }

        [Fact]
        public void WelchPsd_FrequencyAxisStartsAtZeroWithBinSpacing()
        {
            var signal = Sine(1.0, 100, 1000, 2048);
            var psd = signal.WelchPsd(segmentSize: 256);

            Assert.Equal(0.0, psd.Frequencies[0], precision: 12);
            Assert.Equal(1000.0 / 256, psd.Frequencies[1], precision: 12);
        }

        [Fact]
        public void WelchPsd_ConcentratesNonAlignedToneBetterThanSingleWindow()
        {
            var signal = Sine(1.0, 90, 1000, 4096); // 90 Hz on neither estimator's bin grid.
            var welch = signal.WelchPsd(segmentSize: 512, hopSize: 256); // Hann window by default.
            var single = signal.PowerSpectralDensity();

            // With a Hann window Welch suppresses sidelobes (~-31 dB) so nearly all energy sits within
            // ±5 Hz of the tone, whereas the rectangular single window leaks across the spectrum.
            double welchOut = OutOfBandFraction(welch.Power, 1000, 512, 90);
            double singleOut = OutOfBandFraction(single, 1000, 4096, 90);

            Assert.True(welchOut < singleOut);
            Assert.True(welchOut < 0.02, $"Welch out-of-band fraction was {welchOut}.");
        }

        private static double OutOfBandFraction(double[] power, double samplingRate, int binCount, double toneHz)
        {
            double total = 0;
            foreach (var p in power)
                total += p;
            if (total <= 0)
                return 1;

            double resolution = samplingRate / binCount;
            double outside = 0;
            for (int i = 0; i < power.Length; i++)
            {
                double hz = i * resolution;
                if (Math.Abs(hz - toneHz) > 5)
                    outside += power[i];
            }
            return outside / total;
        }

        [Fact]
        public void WelchPsd_NonPowerOfTwoSegment_Throws()
        {
            var signal = Sine(1.0, 100, 1000, 2048);
            Assert.Throws<ArgumentOutOfRangeException>(() => signal.WelchPsd(segmentSize: 300));
        }

        [Fact]
        public void WelchPsd_SegmentLargerThanSignal_Throws()
        {
            var signal = Sine(1.0, 100, 1000, 2048);
            Assert.Throws<ArgumentException>(() => signal.WelchPsd(segmentSize: 4096));
        }

        [Fact]
        public void WelchPsd_TinySignal_Throws()
        {
            var signal = Sine(1.0, 100, 1000, 3);
            Assert.Throws<ArgumentException>(() => signal.WelchPsd());
        }
    }
}