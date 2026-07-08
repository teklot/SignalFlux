using System;
using UnitsNet.Units;
using Xunit;
using SignalFlux.TimeSeries;

namespace SignalFlux.Tests
{
    public class TimeSeriesTests
    {
        private Signal<double> CreateTestSignal()
        {
            var samples = new double[100];
            for (int i = 0; i < 100; i++)
                samples[i] = Math.Sin(2 * Math.PI * i / 100);
            return new Signal<double>(samples, 100, Timestamp.Now, ElectricPotentialUnit.Volt);
        }

        [Fact]
        public void Resample_HalfFrequency_HalfCount()
        {
            var signal = CreateTestSignal();
            var resampled = signal.Resample(50);

            Assert.Equal(50, resampled.Count);
            Assert.Equal(50, resampled.Frequency);
        }

        [Fact]
        public void Resample_DoubleFrequency_DoubleCount()
        {
            var signal = CreateTestSignal();
            var resampled = signal.Resample(200);

            Assert.Equal(200, resampled.Count);
            Assert.Equal(200, resampled.Frequency);
        }

        [Fact]
        public void Window_ByIndex_ReturnsCorrectSubset()
        {
            var signal = CreateTestSignal();
            var windowed = signal.Window(10, 20);

            Assert.Equal(20, windowed.Count);
        }

        [Fact]
        public void Statistics_ReturnsCorrectValues()
        {
            var samples = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var signal = new Signal<double>(samples, 10, Timestamp.Now);
            var stats = signal.Statistics();

            Assert.Equal(5, stats.Count);
            Assert.Equal(3.0, stats.Mean);
            Assert.Equal(1.0, stats.Minimum);
            Assert.Equal(5.0, stats.Maximum);
            Assert.Equal(4.0, stats.Range);
        }

        [Fact]
        public void Downsample_ByFactor2_ReducesCount()
        {
            var samples = new double[] { 1, 2, 3, 4, 5, 6 };
            var signal = new Signal<double>(samples, 100, Timestamp.Now);
            var downsampled = signal.Downsample(2);

            Assert.Equal(3, downsampled.Count);
        }

        [Fact]
        public void Normalize_ReturnsZeroToOneRange()
        {
            var signal = new Signal<double>(new double[] { 10, 20, 30, 40, 50 }, 100, Timestamp.Now);
            var normalized = signal.Normalize();
            var stats = normalized.Statistics();

            Assert.Equal(0.0, stats.Minimum, 4);
            Assert.Equal(1.0, stats.Maximum, 4);
        }
    }
}
