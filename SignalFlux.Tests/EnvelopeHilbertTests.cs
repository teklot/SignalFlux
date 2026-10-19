using System;
using System.Linq;
using Xunit;
using SignalFlux;
using SignalFlux.SignalProcessing;

namespace SignalFlux.Tests
{
    public class EnvelopeHilbertTests
    {
        private static Signal<double> AmCarrier(double modFrequency, double carrierFrequency, double sampleRate, int samples)
        {
            var data = new double[samples];
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / sampleRate;
                data[i] = (1 + 0.8 * Math.Sin(2 * Math.PI * modFrequency * t)) * Math.Sin(2 * Math.PI * carrierFrequency * t);
            }
            return new Signal<double>(data, sampleRate, Timestamp.UtcNow, unit: null);
        }

        [Fact]
        public void Envelope_TracksAmplitudeModulation()
        {
            var signal = AmCarrier(modFrequency: 2, carrierFrequency: 100, sampleRate: 1000, samples: 2000);
            var envelope = signal.Envelope();

            // Hilbert envelope of (1 + 0.8 sin(2 pi 2 t)) * sin(...) recovers (1 +- 0.8).
            // Circular-boundary artifacts affect the ends; inspect the middle half only.
            int start = signal.Count / 4;
            int end = 3 * signal.Count / 4;
            var samples = envelope.Samples.Span;
            double min = double.MaxValue, max = double.MinValue;
            for (int i = start; i < end; i++)
            {
                min = Math.Min(min, samples[i]);
                max = Math.Max(max, samples[i]);
            }

            Assert.InRange(min, 0.1, 0.4); // low ~= 1 - 0.8
            Assert.InRange(max, 1.6, 2.0); // high ~= 1 + 0.8
        }

        [Fact]
        public void Envelope_OfPureToneIsConstant()
        {
            var data = new double[1024];
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Sin(2 * Math.PI * 100 * i / 1000.0);
            var signal = new Signal<double>(data, 1000, Timestamp.UtcNow);

            var envelope = signal.Envelope();
            var samples = envelope.Samples.Span;
            for (int i = 100; i < 900; i++)
                Assert.InRange(samples[i], 0.9, 1.1);
        }

        [Fact]
        public void Envelope_PreservesMetadata()
        {
            var signal = new Signal<double>(new double[] { 0, 1, 0, -1, 0 }, 1000, Timestamp.UtcNow);
            var envelope = signal.Envelope();

            Assert.Equal(signal.Count, envelope.Count);
            Assert.Equal(signal.Frequency, envelope.Frequency);
            Assert.Equal(signal.StartTime, envelope.StartTime);
        }

        [Fact]
        public void Envelope_EmptySignal_ReturnsEmpty()
        {
            var signal = new Signal<double>(Array.Empty<double>(), 1000, Timestamp.UtcNow);
            Assert.Equal(0, signal.Envelope().Count);
        }

        [Fact]
        public void Envelope_SingleSample_ReturnsSameValue()
        {
            var signal = new Signal<double>(new double[] { 2.5 }, 1000, Timestamp.UtcNow);
            Assert.Equal(2.5, signal.Envelope().Samples.Span[0], precision: 12);
        }
    }
}