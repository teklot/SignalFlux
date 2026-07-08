using Xunit;

namespace SignalFlux.Tests
{
    public class SessionTests
    {
        [Fact]
        public void CreateSession_Succeeds()
        {
            var session = new Session("session-001");

            Assert.Equal("session-001", session.Id);
            Assert.Empty(session.Experiments);
            Assert.False(session.CanReplay);
        }

        [Fact]
        public void WithExperiment_AddsExperiment()
        {
            var session = new Session("s1");
            var exp = new Experiment("exp-001");
            var updated = session.WithExperiment(exp);

            Assert.Single(updated.Experiments);
            Assert.Empty(session.Experiments);
        }

        [Fact]
        public void WithAnnotation_AddsAnnotation()
        {
            var session = new Session("s1");
            var updated = session.WithAnnotation("Test completed");

            Assert.Single(updated.Annotations);
            Assert.Contains("Test completed", updated.Annotations);
        }
    }
}
