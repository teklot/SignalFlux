using System;
using NWaves.Operations;
using NWaves.Signals;
using NWaves.Utils;
using NWaves.Windows;

namespace SignalFlux.SignalProcessing
{
    /// <summary>Result of a Welch power spectral density estimate: a power value per frequency bin.</summary>
    public readonly struct WelchPsdResult
    {
        /// <summary>The center frequency (Hz) of each bin.</summary>
        public double[] Frequencies { get; }
        /// <summary>The estimated power density (V^2 / Hz with the default density scaling) per bin.</summary>
        public double[] Power { get; }

        internal WelchPsdResult(double[] frequencies, double[] power)
        {
            Frequencies = frequencies;
            Power = power;
        }
    }

    /// <summary>
    /// Power spectral density estimation via Welch's averaged periodogram, delegated to NWaves. Segments are
    /// windowed and averaged, which yields a smoother estimate than the single-window |F|^2 / N estimator.
    /// </summary>
    public static class PsdExtensions
    {
        /// <summary>
        /// Computes the Welch power spectral density of the signal using power-of-two segments and the given window.
        /// </summary>
        /// <param name="signal">The source signal (at least 4 samples).</param>
        /// <param name="segmentSize">FFT segment length; a power of two no larger than the sample count (default: a power of two near the signal length, capped at 1024).</param>
        /// <param name="hopSize">Hop between segments (default: half the segment size, i.e. 50% overlap).</param>
        /// <param name="window">Window applied to each segment (default Hann).</param>
        /// <returns>A <see cref="WelchPsdResult"/> with a frequency axis in Hz and power per bin.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the signal frequency or segment/hop parameters are invalid.</exception>
        /// <exception cref="ArgumentException">Thrown when the segment size exceeds the sample count.</exception>
        public static WelchPsdResult WelchPsd(
            this Signal<double> signal,
            int segmentSize = 0,
            int hopSize = 0,
            WindowType window = WindowType.Hann)
        {
            if (signal.Frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(signal), "Signal frequency must be positive.");
            if (signal.Count < 4)
                throw new ArgumentException("Welch PSD requires at least 4 samples.", nameof(signal));

            if (segmentSize <= 0)
            {
                int cap = Math.Min(signal.Count, 1024);
                segmentSize = LargestPowerOfTwoAtMost(cap);
            }
            if (segmentSize > signal.Count)
                throw new ArgumentException("Segment size cannot exceed the sample count.", nameof(segmentSize));
            if ((segmentSize & (segmentSize - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(segmentSize), "Segment size must be a power of two.");

            if (hopSize <= 0)
                hopSize = segmentSize / 2;
            if (hopSize > segmentSize)
                throw new ArgumentOutOfRangeException(nameof(hopSize), "Hop size cannot exceed the segment size.");

            var samples = signal.Samples.ToArray().ToFloats();
            var discrete = new DiscreteSignal((int)Math.Round(signal.Frequency), samples);
            float[] periodogram = Operation.Welch(discrete, segmentSize, hopSize, window, segmentSize, (int)Math.Round(signal.Frequency));

            var power = new double[periodogram.Length];
            var frequencies = new double[periodogram.Length];
            for (int i = 0; i < periodogram.Length; i++)
            {
                power[i] = periodogram[i];
                frequencies[i] = signal.Frequency * i / segmentSize;
            }

            return new WelchPsdResult(frequencies, power);
        }

        private static int LargestPowerOfTwoAtMost(int value)
        {
            int power = 1;
            while (power * 2 <= value)
                power <<= 1;
            return power;
        }
    }
}