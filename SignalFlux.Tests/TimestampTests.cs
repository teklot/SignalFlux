using System;
using Xunit;

namespace SignalFlux.Tests
{
    public class TimestampTests
    {
        [Fact]
        public void Now_ReturnsRecentTime()
        {
            var now = Timestamp.Now;
            var utcNow = DateTime.UtcNow;

            Assert.True(Math.Abs(utcNow.Ticks - now.Ticks) < TimeSpan.TicksPerSecond);
        }

        [Fact]
        public void FromDateTime_RoundTrips()
        {
            var dt = new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc);
            var ts = Timestamp.FromDateTime(dt);

            Assert.Equal(dt, ts.DateTime);
        }

        [Fact]
        public void UnixTime_ConversionsAreCorrect()
        {
            var ts = Timestamp.FromUnixMilliseconds(1_000_000_000);
            var ms = ts.ToUnixMilliseconds();

            Assert.Equal(1_000_000_000, ms);
        }

        [Fact]
        public void Comparison_OperatorsWork()
        {
            var early = new Timestamp(100);
            var late = new Timestamp(200);

            Assert.True(early < late);
            Assert.True(late > early);
            Assert.True(early <= late);
            Assert.True(late >= early);
        }

        [Fact]
        public void Subtraction_ReturnsTimeSpan()
        {
            var a = new Timestamp(200);
            var b = new Timestamp(100);

            Assert.Equal(TimeSpan.FromTicks(100), a - b);
            Assert.Equal(TimeSpan.FromTicks(-100), b - a);
        }

        [Fact]
        public void Addition_WithTimeSpan_Works()
        {
            var ts = new Timestamp(100);
            var result = ts + TimeSpan.FromTicks(50);

            Assert.Equal(new Timestamp(150), result);
        }
    }
}
