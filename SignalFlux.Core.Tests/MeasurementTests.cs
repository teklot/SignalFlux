using UnitsNet.Units;
using Xunit;

namespace SignalFlux.Tests
{
    public class MeasurementTests
    {
        [Fact]
        public void CreateMeasurement_WithValue_Succeeds()
        {
            var m = new Measurement<double>(42.0, Timestamp.Now, TemperatureUnit.DegreeCelsius);

            Assert.Equal(42.0, m.Value);
            Assert.Equal(TemperatureUnit.DegreeCelsius, m.Unit);
            Assert.Equal(Quality.Good, m.Quality);
        }

        [Fact]
        public void WithMethods_ReturnNewMeasurement()
        {
            var m = new Measurement<double>(10.0, Timestamp.Zero, ElectricCurrentUnit.Ampere);
            var updated = m.WithValue(20.0).WithUnit(ElectricCurrentUnit.Milliampere);

            Assert.Equal(20.0, updated.Value);
            Assert.Equal(ElectricCurrentUnit.Milliampere, updated.Unit);
            Assert.Equal(10.0, m.Value);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var m = new Measurement<double>(3.14, Timestamp.Zero, ElectricPotentialUnit.Volt);
            var str = m.ToString();

            Assert.Contains("3.14", str);
            Assert.Contains("Volt", str);
        }
    }
}
