using System;
using System.Collections.Generic;
using SignalFlux;

// The package namespace shares a name with the ScottPlot library, so all ScottPlot
// types are referenced through global:: aliases to avoid namespace shadowing.

namespace SignalFlux.Console.Visualization.ScottPlot
{
    using SPlot = global::ScottPlot.Plot;
    using SSignal = global::ScottPlot.Plottables.Signal;
    using SScatter = global::ScottPlot.Plottables.Scatter;
    using SVerticalLine = global::ScottPlot.Plottables.VerticalLine;

    /// <summary>Extension methods converting SignalFlux domain types into native ScottPlot 5 plottables.</summary>
    public static class ScottPlotSignalExtensions
    {
        /// <summary>Adds a <see cref="Signal{T}"/> to the plot as a high-performance signal plottable with a real time axis.</summary>
        /// <typeparam name="T">The sample type (e.g., double, float, int).</typeparam>
        /// <param name="plot">The plot to add to.</param>
        /// <param name="signal">The signal to plot.</param>
        /// <param name="label">Optional legend label.</param>
        public static SSignal AddSignal<T>(this SPlot plot, Signal<T> signal, string? label = null)
        {
            var ys = PlotData.ToDoubles(signal.Samples);
            var result = plot.Add.Signal(ys);
            result.Data.XOffset = PlotData.ToOADate(signal.StartTime);
            result.Data.Period = signal.SampleInterval.TotalDays;
            if (!string.IsNullOrEmpty(label))
            {
                result.LegendText = label;
                plot.ShowLegend();
            }
            plot.Axes.DateTimeTicksBottom();
            if (signal.Unit != null)
                plot.YLabel(PlotData.UnitLabel(signal.Unit));
            return result;
        }

        /// <summary>Adds a <see cref="Signal{T}"/> to the plot as a scatter (x/y points) with a real time axis.</summary>
        /// <typeparam name="T">The sample type (e.g., double, float, int).</typeparam>
        /// <param name="plot">The plot to add to.</param>
        /// <param name="signal">The signal to plot.</param>
        /// <param name="label">Optional legend label.</param>
        public static SScatter AddScatter<T>(this SPlot plot, Signal<T> signal, string? label = null)
        {
            var ys = PlotData.ToDoubles(signal.Samples);
            var xs = new double[ys.Length];
            double startX = PlotData.ToOADate(signal.StartTime);
            double periodDays = signal.SampleInterval.TotalDays;
            for (int i = 0; i < ys.Length; i++)
                xs[i] = startX + i * periodDays;

            var result = plot.Add.Scatter(xs, ys);
            result.MarkerSize = 3;
            if (!string.IsNullOrEmpty(label))
            {
                result.LegendText = label;
                plot.ShowLegend();
            }
            plot.Axes.DateTimeTicksBottom();
            if (signal.Unit != null)
                plot.YLabel(PlotData.UnitLabel(signal.Unit));
            return result;
        }

        /// <summary>Adds a series of <see cref="Measurement{T}"/> values to the plot as a scatter.</summary>
        /// <typeparam name="T">The value type (e.g., double, float, int).</typeparam>
        /// <param name="plot">The plot to add to.</param>
        /// <param name="measurements">The measurements to plot.</param>
        /// <param name="label">Optional legend label.</param>
        public static SScatter AddMeasurements<T>(this SPlot plot, IEnumerable<Measurement<T>> measurements, string? label = null)
        {
            var list = new List<Measurement<T>>(measurements);
            var xs = new double[list.Count];
            var ys = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                xs[i] = PlotData.ToOADate(list[i].Timestamp);
                ys[i] = Convert.ToDouble(list[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var result = plot.Add.Scatter(xs, ys);
            result.MarkerSize = 4;
            if (!string.IsNullOrEmpty(label))
            {
                result.LegendText = label;
                plot.ShowLegend();
            }
            plot.Axes.DateTimeTicksBottom();
            return result;
        }

        /// <summary>Adds vertical dashed lines at each event time, labelled with severity and type.</summary>
        /// <param name="plot">The plot to add to.</param>
        /// <param name="events">The events to annotate.</param>
        public static IReadOnlyList<SVerticalLine> AddEvents(this SPlot plot, IEnumerable<Event> events)
        {
            var lines = new List<SVerticalLine>();
            foreach (var e in events)
            {
                var line = plot.Add.VerticalLine(PlotData.ToOADate(e.Time));
                line.Text = $"[{e.Severity}] {e.Type}";
                line.LinePattern = global::ScottPlot.LinePattern.Dashed;
                line.ExcludeFromLegend = true;
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Adds every numeric signal and event from an <see cref="Experiment"/> to the plot.</summary>
        /// <param name="plot">The plot to add to.</param>
        /// <param name="experiment">The experiment to visualize.</param>
        /// <param name="title">Optional plot title.</param>
        public static SPlot AddExperiment(this SPlot plot, Experiment experiment, string? title = null)
        {
            foreach (var entry in experiment.Signals)
                TryAddSignal(plot, entry.Key, entry.Value);
            plot.AddEvents(experiment.Events);
            if (!string.IsNullOrEmpty(title))
                plot.Title(title);
            return plot;
        }

        private static void TryAddSignal(SPlot plot, string name, object value)
        {
            if (value is Signal<double> sd) plot.AddSignal(sd, name);
            else if (value is Signal<float> sf) plot.AddSignal(sf, name);
            else if (value is Signal<int> si) plot.AddSignal(si, name);
            else if (value is Signal<long> sl) plot.AddSignal(sl, name);
            else if (value is Signal<short> ss) plot.AddSignal(ss, name);
        }
    }
}
