using SignalFlux;
using SignalFlux.Console.Visualization.ScottPlot;
using static System.Console;

namespace SignalFlux.Console
{
    public static class VisualizationSamples
    {
        private const string PlotTitle = "Voltage Signal";
        private const string SeriesLabel = "Voltage";

        private static readonly Timestamp StartTime =
            Timestamp.FromDateTime(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        private static Signal<double> CreateSignal()
        {
            return new Signal<double>(
                new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0 },
                frequency: 10,
                startTime: StartTime,
                unit: UnitsNet.Units.ElectricPotentialUnit.Volt);
        }

        private static Event[] CreateEvents()
        {
            return new[]
            {
                new Event(
                    StartTime + TimeSpan.FromMilliseconds(300),
                    EventSeverity.Warning,
                    "OverTemp",
                    "Temperature exceeded threshold")
            };
        }

        public static void RunScottPlotSample()
        {
            WriteLine("=== ScottPlot Visualization Demo ===");
            WriteLine();

            var plot = new ScottPlot.Plot();
            var plotted = plot.AddSignal(CreateSignal(), SeriesLabel);
            plotted.Color = ScottPlot.Color.FromHex("#1F77B4");
            foreach (var line in plot.AddEvents(CreateEvents()))
                line.Color = ScottPlot.Color.FromHex("#D62728");
            plot.Title(PlotTitle);

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"signalflux-scottplot-{Guid.NewGuid():N}.png");
            plot.SavePng(path, 800, 400);
            WriteLine($"Saved ScottPlot render to: {path}");
            WriteLine();
        }
    }
}
