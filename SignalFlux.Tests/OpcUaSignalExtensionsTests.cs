using System;
using Opc.Ua;
using Xunit;
using SignalFlux.Protocols.OpcUa;

namespace SignalFlux.Tests
{
    public class OpcUaSignalExtensionsTests
    {
        [Fact]
        public void ToMeasurement_ExtractsValueFromDataValue()
        {
            var dv = new DataValue(42.0);
            var measurement = dv.ToMeasurement();

            Assert.Equal(42.0, measurement.Value);
        }

        [Fact]
        public void ToMeasurement_UsesSourceTimestamp()
        {
            var ts = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var dv = new DataValue(1.0, StatusCodes.Good, ts);

            var measurement = dv.ToMeasurement();

            Assert.Equal(Timestamp.FromDateTime(ts), measurement.Timestamp);
        }

        [Fact]
        public void ToMeasurement_ConvertsLocalTimestampToUtc()
        {
            var localTs = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);
            var dv = new DataValue(1.0, StatusCodes.Good, localTs);

            var measurement = dv.ToMeasurement();

            Assert.Equal(localTs.ToUniversalTime().Ticks, measurement.Timestamp.Ticks);
        }

        [Fact]
        public void ToMeasurement_GoodStatusCode_SetsQualityGood()
        {
            var dv = new DataValue(1.0, StatusCodes.Good, DateTime.UtcNow);

            var measurement = dv.ToMeasurement();

            Assert.Equal(Quality.Good, measurement.Quality);
        }

        [Fact]
        public void ToMeasurement_NullDataValue_Throws()
        {
            DataValue? dv = null;
            Assert.Throws<ArgumentNullException>(() => dv!.ToMeasurement());
        }

        [Fact]
        public void ToMeasurement_SetsSourceMetadata()
        {
            var dv = new DataValue(1.0);
            var measurement = dv.ToMeasurement(source: "test-server");

            Assert.Equal("test-server", measurement.Metadata["source"]);
        }

        [Fact]
        public void ToMeasurement_DefaultSource_IsOpcua()
        {
            var dv = new DataValue(1.0);
            var measurement = dv.ToMeasurement();

            Assert.Equal("opcua", measurement.Metadata["source"]);
        }

        [Fact]
        public void ToQuality_GoodCode_ReturnsGood()
        {
            StatusCode code = StatusCodes.Good;
            Assert.Equal(Quality.Good, code.ToQuality());
        }

        [Fact]
        public void ToQuality_UncertainRange_ReturnsFair()
        {
            var code = new StatusCode(0x4000);
            Assert.Equal(Quality.Fair, code.ToQuality());

            var midCode = new StatusCode(0x6000);
            Assert.Equal(Quality.Fair, midCode.ToQuality());

            var upperCode = new StatusCode(0x7FFF);
            Assert.Equal(Quality.Fair, upperCode.ToQuality());
        }

        [Fact]
        public void ToQuality_BadRange_ReturnsBad()
        {
            var code = new StatusCode(0x8000);
            Assert.Equal(Quality.Bad, code.ToQuality());

            var midCode = new StatusCode(0xC000);
            Assert.Equal(Quality.Bad, midCode.ToQuality());

            var maxCode = new StatusCode(0xFFFF);
            Assert.Equal(Quality.Bad, maxCode.ToQuality());
        }

        [Fact]
        public void ToQuality_NonZeroGood_ReturnsGood()
        {
            var code = new StatusCode(0x0001);
            Assert.Equal(Quality.Good, code.ToQuality());
        }

        [Fact]
        public void ToMeasurement_VariantDouble_PreservesValue()
        {
            var dv = new DataValue(new Variant(3.14159));
            var measurement = dv.ToMeasurement();

            Assert.Equal(3.14159, measurement.Value);
        }

        [Fact]
        public void ToMeasurement_VariantInt_ConvertsToDouble()
        {
            var dv = new DataValue(new Variant(100));
            var measurement = dv.ToMeasurement();

            Assert.Equal(100.0, measurement.Value);
        }
    }
}
