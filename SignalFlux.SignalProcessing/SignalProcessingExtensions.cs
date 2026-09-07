using System;
using System.Collections.Generic;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace SignalFlux.SignalProcessing
{
    /// <summary>
    /// Provides signal-processing operations for <see cref="Signal{T}"/> such as FFT/spectrum analysis,
    /// peak detection, power spectral density, and envelope. Numerical algorithms are delegated to
    /// Math.NET Numerics per the project's guiding principle.
    /// </summary>
    public static class SignalProcessingExtensions
    {
        /// <summary>Computes the forward FFT spectrum of the signal as complex frequency bins.</summary>
        /// <param name="signal">The source signal.</param>
        public static Complex[] ForwardSpectrum(this Signal<double> signal)
        {
            if (signal.Count == 0)
                return Array.Empty<Complex>();

            var samples = new Complex[signal.Count];
            var span = signal.Samples.Span;
            for (int i = 0; i < span.Length; i++)
                samples[i] = new Complex(span[i], 0);

            Fourier.Forward(samples, FourierOptions.Matlab);
            return samples;
        }

        /// <summary>Computes the magnitude (amplitude) spectrum of the signal, one bin per sample.</summary>
        /// <param name="signal">The source signal.</param>
        public static double[] MagnitudeSpectrum(this Signal<double> signal)
        {
            var spectrum = signal.ForwardSpectrum();
            var result = new double[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++)
                result[i] = spectrum[i].Magnitude;
            return result;
        }

        /// <summary>Computes the power spectrum of the signal (squared magnitude), one bin per sample.</summary>
        /// <param name="signal">The source signal.</param>
        public static double[] PowerSpectrum(this Signal<double> signal)
        {
            var spectrum = signal.ForwardSpectrum();
            var result = new double[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++)
            {
                var m = spectrum[i].Magnitude;
                result[i] = m * m;
            }
            return result;
        }

        /// <summary>Computes the frequency (Hz) of each FFT bin using the signal's sampling frequency.</summary>
        /// <param name="signal">The source signal.</param>
        public static double[] FrequencyAxis(this Signal<double> signal)
        {
            var result = new double[signal.Count];
            for (int i = 0; i < signal.Count; i++)
                result[i] = (double)i * signal.Frequency / signal.Count;
            return result;
        }

        /// <summary>Reconstructs a time-domain signal from a complex spectrum via the inverse FFT.</summary>
        /// <param name="spectrum">The complex discrete spectrum (length must equal the desired sample count).</param>
        /// <param name="frequency">The sampling frequency in Hz for the reconstructed signal.</param>
        /// <param name="startTime">The UTC time of the first sample.</param>
        /// <param name="unit">The unit of the reconstructed samples.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="frequency"/> is zero or negative.</exception>
        public static Signal<double> InverseSpectrum(
            this Complex[] spectrum,
            double frequency,
            Timestamp startTime,
            Enum? unit = null)
        {
            if (frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive");

            var data = (Complex[])spectrum.Clone();
            Fourier.Inverse(data, FourierOptions.Matlab);

            var samples = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
                samples[i] = data[i].Real;

            return new Signal<double>(samples, frequency, startTime, unit);
        }

        /// <summary>Finds the indices of local maxima in the signal.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="threshold">Values at or below this are never considered peaks.</param>
        /// <param name="minDistance">The minimum number of samples between consecutive peaks.</param>
        public static int[] FindPeaks(this Signal<double> signal, double threshold = 0, int minDistance = 1)
        {
            if (signal.Count == 0)
                return Array.Empty<int>();

            var samples = signal.Samples.Span;
            var peaks = new List<int>();

            for (int i = 1; i < samples.Length - 1; i++)
            {
                if (samples[i] <= threshold)
                    continue;
                if (samples[i] > samples[i - 1] && samples[i] >= samples[i + 1])
                    peaks.Add(i);
            }

            if (minDistance > 1)
                peaks = PruneByDistance(peaks, minDistance);

            return peaks.ToArray();
        }

        /// <summary>Finds local maxima in the signal with their index and value.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="threshold">Values at or below this are never considered peaks.</param>
        /// <param name="minDistance">The minimum number of samples between consecutive peaks.</param>
        public static Peak[] FindPeaksDetailed(this Signal<double> signal, double threshold = 0, int minDistance = 1)
        {
            var indices = signal.FindPeaks(threshold, minDistance);
            var samples = signal.Samples.Span;
            var result = new Peak[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                result[i] = new Peak(indices[i], samples[indices[i]]);
            return result;
        }

        /// <summary>Computes a one-sided power spectral density estimate as |F|^2 / N.</summary>
        /// <param name="signal">The source signal.</param>
        public static double[] PowerSpectralDensity(this Signal<double> signal)
        {
            if (signal.Count == 0)
                return Array.Empty<double>();

            var spectrum = signal.ForwardSpectrum();
            int n = spectrum.Length;
            int half = n / 2 + 1;
            var result = new double[half];
            for (int i = 0; i < half; i++)
            {
                var m = spectrum[i].Magnitude;
                result[i] = (m * m) / n;
            }
            return result;
        }

        /// <summary>Computes a moving-max envelope of the signal using a sliding window.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="windowSize">The sliding window size (should be odd).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="windowSize"/> is non-positive.</exception>
        public static Signal<double> Envelope(this Signal<double> signal, int windowSize)
        {
            if (windowSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be positive");

            var samples = signal.Samples.Span;
            var result = new double[samples.Length];
            int radius = windowSize / 2;

            for (int i = 0; i < samples.Length; i++)
            {
                int start = Math.Max(0, i - radius);
                int end = Math.Min(samples.Length - 1, i + radius);
                double max = samples[start];
                for (int j = start + 1; j <= end; j++)
                    if (samples[j] > max)
                        max = samples[j];
                result[i] = max;
            }

            return signal.WithSamples(result);
        }

        private static List<int> PruneByDistance(List<int> peaks, int minDistance)
        {
            var result = new List<int>();
            int last = -minDistance;
            foreach (var p in peaks)
            {
                if (p - last >= minDistance)
                {
                    result.Add(p);
                    last = p;
                }
            }
            return result;
        }
    }

    /// <summary>Represents a detected peak with its index and value.</summary>
    public readonly struct Peak
    {
        /// <summary>The zero-based index of the peak within the signal.</summary>
        public int Index { get; }
        /// <summary>The value at the peak.</summary>
        public double Value { get; }

        /// <summary>Creates a peak descriptor.</summary>
        /// <param name="index">The zero-based index.</param>
        /// <param name="value">The peak value.</param>
        public Peak(int index, double value)
        {
            Index = index;
            Value = value;
        }
    }
}
