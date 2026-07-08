using Xunit;

namespace SignalFlux.Tests
{
    public class ExperimentTests
    {
        [Fact]
        public void CreateExperiment_Succeeds()
        {
            var exp = new Experiment("exp-001", start: Timestamp.Now);

            Assert.Equal("exp-001", exp.Id);
            Assert.Empty(exp.Signals);
            Assert.Empty(exp.Events);
        }

        [Fact]
        public void Experiment_Equality_ById()
        {
            var a = new Experiment("exp-001");
            var b = new Experiment("exp-001");
            var c = new Experiment("exp-002");

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var exp = new Experiment("exp-001", start: new Timestamp(1000), end: new Timestamp(2000));
            var str = exp.ToString();

            Assert.Contains("exp-001", str);
        }
    }
}
