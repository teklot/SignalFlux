using System;
using UnitsNet.Units;
using Xunit;
using SignalFlux.Protocols.OpcUa;

namespace SignalFlux.Tests
{
    public class OpcUaUnitMapperTests
    {
        [Theory]
        [InlineData("V", ElectricPotentialUnit.Volt)]
        [InlineData("Volt", ElectricPotentialUnit.Volt)]
        [InlineData("mV", ElectricPotentialUnit.Millivolt)]
        [InlineData("kV", ElectricPotentialUnit.Kilovolt)]
        public void TryGetUnit_ElectricPotential_MapsCorrectly(string symbol, ElectricPotentialUnit expected)
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit(symbol);
            Assert.Equal(expected, unit);
        }

        [Theory]
        [InlineData("°C", TemperatureUnit.DegreeCelsius)]
        [InlineData("Celsius", TemperatureUnit.DegreeCelsius)]
        [InlineData("°F", TemperatureUnit.DegreeFahrenheit)]
        [InlineData("K", TemperatureUnit.Kelvin)]
        public void TryGetUnit_Temperature_MapsCorrectly(string symbol, TemperatureUnit expected)
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit(symbol);
            Assert.Equal(expected, unit);
        }

        [Theory]
        [InlineData("Pa", PressureUnit.Pascal)]
        [InlineData("kPa", PressureUnit.Kilopascal)]
        [InlineData("bar", PressureUnit.Bar)]
        [InlineData("psi", PressureUnit.PoundForcePerSquareInch)]
        public void TryGetUnit_Pressure_MapsCorrectly(string symbol, PressureUnit expected)
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit(symbol);
            Assert.Equal(expected, unit);
        }

        [Theory]
        [InlineData("A", ElectricCurrentUnit.Ampere)]
        [InlineData("mA", ElectricCurrentUnit.Milliampere)]
        public void TryGetUnit_Current_MapsCorrectly(string symbol, ElectricCurrentUnit expected)
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit(symbol);
            Assert.Equal(expected, unit);
        }

        [Theory]
        [InlineData("Hz", FrequencyUnit.Hertz)]
        [InlineData("W", PowerUnit.Watt)]
        [InlineData("kWh", EnergyUnit.KilowattHour)]
        [InlineData("m/s", SpeedUnit.MeterPerSecond)]
        [InlineData("km/h", SpeedUnit.KilometerPerHour)]
        [InlineData("kg", MassUnit.Kilogram)]
        [InlineData("deg", AngleUnit.Degree)]
        public void TryGetUnit_CommonUnits_MapsCorrectly(string symbol, Enum expected)
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit(symbol);
            Assert.Equal(expected, unit);
        }

        [Fact]
        public void TryGetUnit_IsCaseInsensitive()
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit("BAR");
            Assert.Equal(PressureUnit.Bar, unit);

            unit = OpcUaUnitMapper.TryGetUnit("volt");
            Assert.Equal(ElectricPotentialUnit.Volt, unit);
        }

        [Fact]
        public void TryGetUnit_TrimsWhitespace()
        {
            Enum unit = OpcUaUnitMapper.TryGetUnit("  °C  ");
            Assert.Equal(TemperatureUnit.DegreeCelsius, unit);
        }

        [Fact]
        public void TryGetUnit_UnknownSymbol_ReturnsNull()
        {
            Assert.Null(OpcUaUnitMapper.TryGetUnit("furlongs"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryGetUnit_NullOrWhitespace_ReturnsNull(string? input)
        {
            Assert.Null(OpcUaUnitMapper.TryGetUnit(input));
        }
    }

    public class OpcUaConnectionOptionsTests
    {
        [Fact]
        public void Defaults_AreSensible()
        {
            var options = new OpcUaConnectionOptions();

            Assert.Equal("SignalFlux", options.ApplicationName);
            Assert.False(options.UseSecurity);
            Assert.Null(options.UserCredentials);
            Assert.True(options.AutoAcceptUntrustedCertificates);
            Assert.True(options.CreateApplicationCertificate);
            Assert.Equal(60_000, options.SessionTimeoutMs);
            Assert.Equal(15_000, options.OperationTimeoutMs);
            Assert.Equal(5_000, options.ReconnectPeriodMs);
        }

        [Fact]
        public void UserCredentials_UserNamePassword_ArePreserved()
        {
            var credentials = new OpcUaUserCredentials("operator", "secret");
            var options = new OpcUaConnectionOptions { UserCredentials = credentials };

            Assert.Same(credentials, options.UserCredentials);
            Assert.Equal("operator", options.UserCredentials.UserName);
            Assert.Equal("secret", options.UserCredentials.Password);
        }
    }
}
