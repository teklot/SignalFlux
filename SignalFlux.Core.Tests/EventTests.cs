using Xunit;

namespace SignalFlux.Tests
{
    public class EventTests
    {
        [Fact]
        public void CreateEvent_Succeeds()
        {
            var evt = new Event(Timestamp.Now, EventSeverity.Warning, "Overheat", "Temperature exceeds threshold", "Sensor1");

            Assert.Equal("Overheat", evt.Type);
            Assert.Equal(EventSeverity.Warning, evt.Severity);
            Assert.Equal("Sensor1", evt.Source);
        }

        [Fact]
        public void Equals_SameValues_AreEqual()
        {
            var t = new Timestamp(1000);
            var a = new Event(t, EventSeverity.Info, "Test", "desc");
            var b = new Event(t, EventSeverity.Info, "Test", "desc");

            Assert.Equal(a, b);
        }

        [Fact]
        public void Equals_DifferentSeverity_AreNotEqual()
        {
            var t = new Timestamp(1000);
            var a = new Event(t, EventSeverity.Info, "Test", "desc");
            var b = new Event(t, EventSeverity.Error, "Test", "desc");

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var evt = new Event(Timestamp.Zero, EventSeverity.Critical, "Failure", "System failure");
            var str = evt.ToString();

            Assert.Contains("Critical", str);
            Assert.Contains("Failure", str);
        }
    }
}
