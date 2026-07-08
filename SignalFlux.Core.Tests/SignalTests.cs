using System;
using Xunit;

namespace SignalFlux.Tests
{
    public class SignalTests
    {
        [Fact]
        public void CreateSignal_WithValidParameters_Succeeds()
        {
            var samples = new double[] { 1.0, 2.0, 3.0, 4.0 };
            var signal = new Signal<double>(samples, 100, Timestamp.Now);

            Assert.Equal(4, signal.Count);
            Assert.Equal(100, signal.Frequency);
        }

        [Fact]
        public void CreateSignal_WithNegativeFrequency_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Signal<double>(new double[] { 1.0 }, -1, Timestamp.Now));
        }

        [Fact]
        public void WithMethods_ReturnNewSignalWithUpdatedProperty()
        {
            var signal = new Signal<double>(new double[] { 1.0 }, 100, Timestamp.Zero);
            var updated = signal.WithFrequency(200);

            Assert.Equal(200, updated.Frequency);
            Assert.Equal(100, signal.Frequency);
        }

        [Fact]
        public void Duration_IsCorrect()
        {
            var samples = new double[1000];
            var signal = new Signal<double>(samples, 100, Timestamp.Now);

            Assert.Equal(10.0, signal.Duration.TotalSeconds);
        }

        [Fact]
        public void EndTime_IsCorrect()
        {
            var start = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var samples = new double[100];
            var signal = new Signal<double>(samples, 10, start);

            Assert.Equal(start + TimeSpan.FromSeconds(10), signal.EndTime);
        }

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var t = Timestamp.Now;
            var a = new Signal<double>(new double[] { 1, 2 }, 100, t);
            var b = new Signal<double>(new double[] { 1, 2 }, 100, t);

            Assert.Equal(a, b);
            Assert.True(a == b);
        }

        [Fact]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var t = Timestamp.Now;
            var a = new Signal<double>(new double[] { 1, 2 }, 100, t);
            var b = new Signal<double>(new double[] { 1, 3 }, 100, t);

            Assert.NotEqual(a, b);
            Assert.True(a != b);
        }
    }
}
