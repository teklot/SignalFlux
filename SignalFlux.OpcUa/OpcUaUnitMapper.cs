using System;
using System.Collections.Generic;
using System.Globalization;
using UnitsNet.Units;

namespace SignalFlux.Protocols.OpcUa
{
    /// <summary>Maps OPC UA engineering units (EUInformation display names and symbols) to typed UnitsNet unit enums.</summary>
    public static class OpcUaUnitMapper
    {
        private static readonly Dictionary<string, Enum> Map = BuildMap(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Attempts to map an OPC UA engineering unit to a typed UnitsNet unit enum.
        /// </summary>
        /// <param name="displayNameOrSymbol">The EUInformation display name or symbol (e.g., "°C", "Volt", "kWh").</param>
        /// <returns>The matching UnitsNet unit enum, or null when no mapping is known.</returns>
        public static Enum TryGetUnit(string displayNameOrSymbol)
        {
            if (string.IsNullOrWhiteSpace(displayNameOrSymbol)) return null;

            return Map.TryGetValue(displayNameOrSymbol.Trim(), out var unit) ? unit : null;
        }

        internal static Dictionary<string, Enum> BuildMap(IEqualityComparer<string> comparer)
        {
            var map = new Dictionary<string, Enum>(comparer)
            {
                // Electric potential
                ["V"] = ElectricPotentialUnit.Volt,
                ["Volt"] = ElectricPotentialUnit.Volt,
                ["mV"] = ElectricPotentialUnit.Millivolt,
                ["Millivolt"] = ElectricPotentialUnit.Millivolt,
                ["kV"] = ElectricPotentialUnit.Kilovolt,

                // Temperature
                ["°C"] = TemperatureUnit.DegreeCelsius,
                ["DegC"] = TemperatureUnit.DegreeCelsius,
                ["Celsius"] = TemperatureUnit.DegreeCelsius,
                ["°F"] = TemperatureUnit.DegreeFahrenheit,
                ["DegF"] = TemperatureUnit.DegreeFahrenheit,
                ["Fahrenheit"] = TemperatureUnit.DegreeFahrenheit,
                ["K"] = TemperatureUnit.Kelvin,
                ["Kelvin"] = TemperatureUnit.Kelvin,

                // Pressure
                ["Pa"] = PressureUnit.Pascal,
                ["Pascal"] = PressureUnit.Pascal,
                ["hPa"] = PressureUnit.Hectopascal,
                ["kPa"] = PressureUnit.Kilopascal,
                ["MPa"] = PressureUnit.Megapascal,
                ["bar"] = PressureUnit.Bar,
                ["mbar"] = PressureUnit.Millibar,
                ["psi"] = PressureUnit.PoundForcePerSquareInch,

                // Current
                ["A"] = ElectricCurrentUnit.Ampere,
                ["Ampere"] = ElectricCurrentUnit.Ampere,
                ["mA"] = ElectricCurrentUnit.Milliampere,

                // Frequency
                ["Hz"] = FrequencyUnit.Hertz,
                ["Hertz"] = FrequencyUnit.Hertz,
                ["kHz"] = FrequencyUnit.Kilohertz,
                ["MHz"] = FrequencyUnit.Megahertz,

                // Power
                ["W"] = PowerUnit.Watt,
                ["Watt"] = PowerUnit.Watt,
                ["kW"] = PowerUnit.Kilowatt,
                ["MW"] = PowerUnit.Megawatt,

                // Energy
                ["J"] = EnergyUnit.Joule,
                ["Joule"] = EnergyUnit.Joule,
                ["kWh"] = EnergyUnit.KilowattHour,

                // Speed
                ["m/s"] = SpeedUnit.MeterPerSecond,
                ["km/h"] = SpeedUnit.KilometerPerHour,
                ["mph"] = SpeedUnit.MilePerHour,
                ["kn"] = SpeedUnit.Knot,

                // Length
                ["m"] = LengthUnit.Meter,
                ["Meter"] = LengthUnit.Meter,
                ["mm"] = LengthUnit.Millimeter,
                ["cm"] = LengthUnit.Centimeter,
                ["km"] = LengthUnit.Kilometer,

                // Mass
                ["kg"] = MassUnit.Kilogram,
                ["Kilogram"] = MassUnit.Kilogram,
                ["g"] = MassUnit.Gram,
                ["t"] = MassUnit.Tonne,

                // Angle
                ["deg"] = AngleUnit.Degree,
                ["Degree"] = AngleUnit.Degree,
                ["rad"] = AngleUnit.Radian,
            };
            return map;
        }
    }
}
