using System;
using System.Globalization;

namespace SignalFlux.Console.Visualization
{
    /// <summary>Internal helpers for converting SignalFlux domain types into plot-ready values.</summary>
    internal static class PlotData
    {
        /// <summary>Converts numeric samples into a double[] suitable for plotting.</summary>
        /// <typeparam name="T">The sample type (e.g., double, float, int, short).</typeparam>
        /// <param name="samples">The sample memory.</param>
        public static double[] ToDoubles<T>(ReadOnlyMemory<T> samples)
        {
            var span = samples.Span;
            var result = new double[span.Length];
            for (int i = 0; i < span.Length; i++)
                result[i] = Convert.ToDouble(span[i], CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>Converts a timestamp to an OLE Automation date (OADate), the convention shared by ScottPlot and OxyPlot time axes.</summary>
        /// <param name="timestamp">The timestamp.</param>
        public static double ToOADate(Timestamp timestamp) => timestamp.DateTime.ToOADate();

        /// <summary>Returns a short axis label for a UnitsNet unit enum, or "value" when no unit is set.</summary>
        /// <param name="unit">The unit enum (e.g., <c>ElectricPotentialUnit.Volt</c>).</param>
        public static string UnitLabel(Enum unit) => unit == null ? "value" : unit.ToString();
    }
}
