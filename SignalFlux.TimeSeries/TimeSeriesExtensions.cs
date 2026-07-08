using System;
using System.Collections.Generic;
using System.Linq;

namespace SignalFlux.TimeSeries
{
    /// <summary>Provides extension methods for signal processing and analysis on <see cref="Signal{T}"/>.</summary>
    public static class TimeSeriesExtensions
    {
        /// <summary>Resamples the signal to a new sampling frequency using the specified interpolation method.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="targetFrequency">The desired sampling frequency in Hz.</param>
        /// <param name="method">The interpolation method (Linear or Nearest).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="targetFrequency"/> is zero or negative.</exception>
        public static Signal<double> Resample(this Signal<double> signal, double targetFrequency, InterpolationMethod method = InterpolationMethod.Linear)
        {
            if (targetFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetFrequency), "Target frequency must be positive");
            if (signal.Count == 0)
                return signal;

            int targetCount = (int)(signal.Count * targetFrequency / signal.Frequency);
            if (targetCount <= 0) targetCount = 1;

            var samples = signal.Samples.Span;
            var result = new double[targetCount];
            double ratio = (double)(signal.Count - 1) / (targetCount - 1);

            for (int i = 0; i < targetCount; i++)
            {
                double srcIdx = i * ratio;
                int idxLow = (int)srcIdx;
                int idxHigh = Math.Min(idxLow + 1, signal.Count - 1);
                double frac = srcIdx - idxLow;

                result[i] = method == InterpolationMethod.Nearest
                    ? (frac < 0.5 ? samples[idxLow] : samples[idxHigh])
                    : samples[idxLow] + (samples[idxHigh] - samples[idxLow]) * frac;
            }

            return signal.WithSamples(result).WithFrequency(targetFrequency);
        }

        /// <summary>Extracts a contiguous window of samples by index range.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="startIndex">The zero-based start index.</param>
        /// <param name="count">The number of samples to include.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified range is invalid.</exception>
        public static Signal<T> Window<T>(this Signal<T> signal, int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= signal.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (count <= 0 || startIndex + count > signal.Count)
                throw new ArgumentOutOfRangeException(nameof(count));

            return signal.WithSamples(signal.Samples.Slice(startIndex, count));
        }

        /// <summary>Extracts a contiguous window of samples by time range.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="start">The start timestamp.</param>
        /// <param name="end">The end timestamp.</param>
        public static Signal<T> Window<T>(this Signal<T> signal, Timestamp start, Timestamp end)
        {
            int startIndex = signal.GetIndexAtTime(start);
            int endIndex = signal.GetIndexAtTime(end);
            return signal.Window(startIndex, endIndex - startIndex);
        }

        /// <summary>Downsamples the signal by averaging (or taking the max/min) over groups of samples.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="factor">The downsampling factor (number of samples to combine).</param>
        /// <param name="method">The downsampling method (Mean, Max, or Min).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="factor"/> is zero or negative.</exception>
        public static Signal<double> Downsample(this Signal<double> signal, int factor, DownsampleMethod method = DownsampleMethod.Mean)
        {
            if (factor <= 0)
                throw new ArgumentOutOfRangeException(nameof(factor));
            if (signal.Count == 0)
                return signal;

            var samples = signal.Samples.Span;
            int resultCount = signal.Count / factor;
            var result = new double[resultCount];

            for (int i = 0; i < resultCount; i++)
            {
                double sum = 0;
                int start = i * factor;

                for (int j = 0; j < factor && start + j < signal.Count; j++)
                    sum += samples[start + j];

                result[i] = method == DownsampleMethod.Max
                    ? MaxInRange(samples, start, factor)
                    : method == DownsampleMethod.Min
                        ? MinInRange(samples, start, factor)
                        : sum / Math.Min(factor, signal.Count - start);
            }

            return signal.WithSamples(result);
        }

        /// <summary>Aligns two signals by time, producing a signal of measurements annotated with the aligned target value.</summary>
        /// <param name="source">The source signal.</param>
        /// <param name="target">The target signal to align to.</param>
        public static Signal<Measurement<T>> Align<T>(this Signal<T> source, Signal<T> target)
        {
            if (source.Count == 0 || target.Count == 0)
                return new Signal<Measurement<T>>(Array.Empty<Measurement<T>>(), 0, Timestamp.Zero);

            var srcSpan = source.Samples.Span;
            var tgtSpan = target.Samples.Span;
            var result = new Measurement<T>[source.Count];

            double ratio = (double)(target.Count - 1) / (source.Count - 1);

            for (int i = 0; i < source.Count; i++)
            {
                double tgtIdx = i * ratio;
                int idxLow = (int)tgtIdx;
                int idxHigh = Math.Min(idxLow + 1, target.Count - 1);
                double frac = tgtIdx - idxLow;

                T alignedValue = frac < 0.5 ? tgtSpan[idxLow] : tgtSpan[idxHigh];
                var time = source.StartTime + TimeSpan.FromSeconds(i / source.Frequency);
                result[i] = new Measurement<T>(srcSpan[i], time, source.Unit)
                    .WithMetadata(new Metadata().With("AlignedTo", alignedValue));
            }

            return new Signal<Measurement<T>>(
                result,
                source.Frequency,
                source.StartTime,
                source.Unit);
        }

        /// <summary>Normalizes the signal values to the [0, 1] range using min-max scaling.</summary>
        /// <param name="signal">The source signal.</param>
        public static Signal<double> Normalize(this Signal<double> signal)
        {
            if (signal.Count == 0) return signal;

            var samples = signal.Samples.Span;
            double min = samples[0], max = samples[0];

            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] < min) min = samples[i];
                if (samples[i] > max) max = samples[i];
            }

            double range = max - min;
            if (Math.Abs(range) < double.Epsilon)
                return signal.WithSamples(new double[samples.Length]);

            var result = new double[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                result[i] = (samples[i] - min) / range;

            return signal.WithSamples(result);
        }

        /// <summary>Merges two signals with the same frequency by combining their samples.</summary>
        /// <param name="a">The first signal.</param>
        /// <param name="b">The second signal.</param>
        /// <param name="method">The merge strategy (Overwrite or Average).</param>
        /// <exception cref="InvalidOperationException">Thrown if the signals have different frequencies.</exception>
        public static Signal<double> Merge(this Signal<double> a, Signal<double> b, MergeMethod method = MergeMethod.Overwrite)
        {
            if (Math.Abs(a.Frequency - b.Frequency) > 0.001)
                throw new InvalidOperationException("Cannot merge signals with different frequencies");

            var aSpan = a.Samples.Span;
            var bSpan = b.Samples.Span;
            int maxLen = Math.Max(a.Count, b.Count);
            var result = new double[maxLen];

            for (int i = 0; i < maxLen; i++)
            {
                bool hasA = i < a.Count;
                bool hasB = i < b.Count;

                if (!hasA) result[i] = bSpan[i];
                else if (!hasB) result[i] = aSpan[i];
                else result[i] = method == MergeMethod.Overwrite ? bSpan[i] : (aSpan[i] + bSpan[i]) / 2;
            }

            return a.WithSamples(result);
        }

        /// <summary>Computes summary statistics (count, mean, median, stddev, min, max, range, sum) for the signal.</summary>
        /// <param name="signal">The source signal.</param>
        public static SignalStatistics Statistics(this Signal<double> signal)
        {
            if (signal.Count == 0)
                return new SignalStatistics();

            var samples = signal.Samples.Span;
            double sum = 0, sumSq = 0;
            double min = samples[0], max = samples[0];

            for (int i = 0; i < samples.Length; i++)
            {
                double v = samples[i];
                sum += v;
                sumSq += v * v;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            double mean = sum / samples.Length;
            double variance = (sumSq / samples.Length) - (mean * mean);

            return new SignalStatistics
            {
                Count = samples.Length,
                Mean = mean,
                Median = Median(samples),
                StandardDeviation = Math.Sqrt(Math.Max(0, variance)),
                Minimum = min,
                Maximum = max,
                Range = max - min,
                Sum = sum
            };
        }

        private static int GetIndexAtTime<T>(this Signal<T> signal, Timestamp time)
        {
            double offsetSeconds = (time - signal.StartTime).TotalSeconds;
            int index = (int)(offsetSeconds * signal.Frequency);
            return index < 0 ? 0 : (index >= signal.Count ? signal.Count - 1 : index);
        }

        private static double Median(ReadOnlySpan<double> sorted)
        {
            var arr = sorted.ToArray();
            Array.Sort(arr);
            int mid = arr.Length / 2;
            return arr.Length % 2 == 0 ? (arr[mid - 1] + arr[mid]) / 2.0 : arr[mid];
        }

        private static double MaxInRange(ReadOnlySpan<double> values, int start, int count)
        {
            double max = values[start];
            for (int i = 1; i < count && start + i < values.Length; i++)
                if (values[start + i] > max) max = values[start + i];
            return max;
        }

        private static double MinInRange(ReadOnlySpan<double> values, int start, int count)
        {
            double min = values[start];
            for (int i = 1; i < count && start + i < values.Length; i++)
                if (values[start + i] < min) min = values[start + i];
            return min;
        }
    }

    /// <summary>Specifies the interpolation method for resampling.</summary>
    public enum InterpolationMethod
    {
        /// <summary>Linear interpolation between adjacent samples.</summary>
        Linear,
        /// <summary>Nearest-neighbor interpolation.</summary>
        Nearest
    }

    /// <summary>Specifies the method used when downsampling a signal.</summary>
    public enum DownsampleMethod
    {
        /// <summary>Averages the samples in each group.</summary>
        Mean,
        /// <summary>Takes the maximum value from each group.</summary>
        Max,
        /// <summary>Takes the minimum value from each group.</summary>
        Min
    }

    /// <summary>Specifies the merge strategy when combining two signals.</summary>
    public enum MergeMethod
    {
        /// <summary>The second signal overwrites the first where samples overlap.</summary>
        Overwrite,
        /// <summary>The two signals are averaged where samples overlap.</summary>
        Average
    }

    /// <summary>Contains summary statistics computed from a signal.</summary>
    public struct SignalStatistics
    {
        /// <summary>The number of samples.</summary>
        public int Count { get; set; }
        /// <summary>The arithmetic mean.</summary>
        public double Mean { get; set; }
        /// <summary>The median value.</summary>
        public double Median { get; set; }
        /// <summary>The population standard deviation.</summary>
        public double StandardDeviation { get; set; }
        /// <summary>The minimum sample value.</summary>
        public double Minimum { get; set; }
        /// <summary>The maximum sample value.</summary>
        public double Maximum { get; set; }
        /// <summary>The range (maximum - minimum).</summary>
        public double Range { get; set; }
        /// <summary>The sum of all sample values.</summary>
        public double Sum { get; set; }
    }
}
