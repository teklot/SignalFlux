using System;
using NWaves.Filters.Base64;

namespace SignalFlux.SignalProcessing
{
    /// <summary>Identifies the analog-prototype-based IIR filter design family.</summary>
    public enum FilterDesignKind
    {
        /// <summary>Maximally flat passband magnitude response (default).</summary>
        Butterworth,
        /// <summary>Equiripple passband with a configurable ripple (1 dB default).</summary>
        ChebyshevI,
        /// <summary>Equiripple stopband (inverse Chebyshev).</summary>
        ChebyshevII,
        /// <summary>Maximally flat group delay (linear-phase-like passband).</summary>
        Bessel,
        /// <summary>Equiripple both bands with passband ripple and stopband attenuation (1 dB / 20 dB default).</summary>
        Elliptic
    }

    /// <summary>
    /// FIR / IIR filtering for <see cref="Signal{T}"/>. Filter design is delegated to NWaves
    /// (Butterworth, Bessel, Chebyshev I/II, Elliptic prototypes); filtering runs on the 64-bit
    /// NWaves filter engines (<see cref="IirFilter64"/>/<see cref="FirFilter64"/>) so coefficient
    /// precision matches the double samples of <see cref="Signal{T}"/>.
    /// </summary>
    public static class FilteringExtensions
    {
        /// <summary>Default passband ripple (dB) applied to Chebyshev I and II designs when using the parameterless design kinds.</summary>
        public const double DefaultRippleDecibels = 1.0;
        /// <summary>Default passband ripple (dB) applied to Elliptic designs.</summary>
        public const double DefaultEllipticPassRippleDecibels = 1.0;
        /// <summary>Default stopband attenuation (dB) applied to Elliptic designs.</summary>
        public const double DefaultEllipticStopAttenuationDecibels = 20.0;

        /// <summary>Applies a low-pass filter with the given cutoff frequency.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="cutoffFrequencyHz">Cutoff frequency in Hertz (must be below the Nyquist frequency).</param>
        /// <param name="order">Filter order (must be at least 1).</param>
        /// <param name="kind">Prototype design family.</param>
        /// <returns>A filtered signal with the same sampling metadata and length as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cutoff frequency is outside (0, Nyquist), or <paramref name="order"/> is not positive.</exception>
        public static Signal<double> LowPass(
            this Signal<double> signal,
            double cutoffFrequencyHz,
            int order = 4,
            FilterDesignKind kind = FilterDesignKind.Butterworth)
        {
            var transfer = Design(signal, kind, order, DesignBand.LowPass, cutoffFrequencyHz);
            return Filter(signal, new IirFilter64(transfer));
        }

        /// <summary>Applies a high-pass filter with the given cutoff frequency.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="cutoffFrequencyHz">Cutoff frequency in Hertz (must be below the Nyquist frequency).</param>
        /// <param name="order">Filter order (must be at least 1).</param>
        /// <param name="kind">Prototype design family.</param>
        /// <returns>A filtered signal with the same sampling metadata and length as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cutoff frequency is outside (0, Nyquist), or <paramref name="order"/> is not positive.</exception>
        public static Signal<double> HighPass(
            this Signal<double> signal,
            double cutoffFrequencyHz,
            int order = 4,
            FilterDesignKind kind = FilterDesignKind.Butterworth)
        {
            var transfer = Design(signal, kind, order, DesignBand.HighPass, cutoffFrequencyHz);
            return Filter(signal, new IirFilter64(transfer));
        }

        /// <summary>Applies a band-pass filter spanning the given edge frequencies.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="lowerFrequencyHz">Lower cutoff frequency in Hertz.</param>
        /// <param name="upperFrequencyHz">Upper cutoff frequency in Hertz (must exceed <paramref name="lowerFrequencyHz"/>).</param>
        /// <param name="order">Filter order (must be at least 1).</param>
        /// <param name="kind">Prototype design family.</param>
        /// <returns>A filtered signal with the same sampling metadata and length as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the edge frequencies are invalid, or <paramref name="order"/> is not positive.</exception>
        public static Signal<double> BandPass(
            this Signal<double> signal,
            double lowerFrequencyHz,
            double upperFrequencyHz,
            int order = 4,
            FilterDesignKind kind = FilterDesignKind.Butterworth)
        {
            var transfer = Design(signal, kind, order, DesignBand.BandPass, lowerFrequencyHz, upperFrequencyHz);
            return Filter(signal, new IirFilter64(transfer));
        }

        /// <summary>Applies a band-stop (notch) filter rejecting the span between the given edge frequencies.</summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="lowerFrequencyHz">Lower cutoff frequency in Hertz.</param>
        /// <param name="upperFrequencyHz">Upper cutoff frequency in Hertz (must exceed <paramref name="lowerFrequencyHz"/>).</param>
        /// <param name="order">Filter order (must be at least 1).</param>
        /// <param name="kind">Prototype design family.</param>
        /// <returns>A filtered signal with the same sampling metadata and length as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the edge frequencies are invalid, or <paramref name="order"/> is not positive.</exception>
        public static Signal<double> BandStop(
            this Signal<double> signal,
            double lowerFrequencyHz,
            double upperFrequencyHz,
            int order = 4,
            FilterDesignKind kind = FilterDesignKind.Butterworth)
        {
            var transfer = Design(signal, kind, order, DesignBand.BandStop, lowerFrequencyHz, upperFrequencyHz);
            return Filter(signal, new IirFilter64(transfer));
        }

        /// <summary>
        /// Smooths the signal with a Savitzky-Golay FIR filter, preserving peaks and transients better than a
        /// plain moving average. The kernel is designed by NWaves and applied with reflected boundaries so the
        /// output stays phase-aligned (zero-delay) with the input.
        /// </summary>
        /// <param name="signal">The source signal.</param>
        /// <param name="windowSize">Odd window size in [3, 31].</param>
        /// <param name="derivative">Savitzky-Golay derivative order in [0, 2] (0 smooths the signal).</param>
        /// <returns>A denoised signal with the same sampling metadata and length as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="windowSize"/> or <paramref name="derivative"/> is invalid.</exception>
        public static Signal<double> Denoise(this Signal<double> signal, int windowSize = 9, int derivative = 0)
        {
            if (windowSize < 3 || windowSize > 31 || windowSize % 2 == 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Savitzky-Golay window size must be odd and within [3, 31].");
            if (derivative < 0 || derivative > 2)
                throw new ArgumentOutOfRangeException(nameof(derivative), "Savitzky-Golay derivative order must be in [0, 2].");

            float[] kernelFloats = new NWaves.Filters.SavitzkyGolayFilter(windowSize, derivative).Kernel;
            double total = 0;
            for (int i = 0; i < kernelFloats.Length; i++)
                total += kernelFloats[i];
            if (total == 0)
                throw new InvalidOperationException("Savitzky-Golay kernel unexpectedly sums to zero.");

            var kernel = new double[kernelFloats.Length];
            for (int i = 0; i < kernelFloats.Length; i++)
                kernel[i] = kernelFloats[i];

            var samples = signal.Samples.ToArray();
            var smoothed = new double[samples.Length];
            int half = (kernel.Length - 1) / 2;
            for (int i = 0; i < samples.Length; i++)
            {
                double acc = 0;
                for (int j = 0; j < kernel.Length; j++)
                    acc += kernel[j] * samples[ReflectIndex(i + j - half, samples.Length)];
                smoothed[i] = acc;
            }

            return signal.WithSamples(smoothed);
        }

        private static int ReflectIndex(int index, int length)
        {
            while (index < 0)
                index = -index - 1;
            while (index >= length)
                index = 2 * length - 1 - index;
            return index;
        }

        private enum DesignBand
        {
            LowPass,
            HighPass,
            BandPass,
            BandStop
        }

        private static NWaves.Filters.Base.TransferFunction Design(
            Signal<double> signal,
            FilterDesignKind kind,
            int order,
            DesignBand band,
            double lowerFrequencyHz,
            double upperFrequencyHz = 0)
        {
            if (order < 1)
                throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be at least 1.");

            double fs = signal.Frequency;
            double nyquist = fs / 2.0;
            double normalized(double hz) => hz / fs;

            switch (band)
            {
                case DesignBand.LowPass:
                case DesignBand.HighPass:
                {
                    if (lowerFrequencyHz <= 0 || lowerFrequencyHz >= nyquist)
                        throw new ArgumentOutOfRangeException(nameof(lowerFrequencyHz), $"Cutoff must be within (0, {nyquist}) Hz.");
                    double f = normalized(lowerFrequencyHz);
                    switch (kind)
                    {
                        case FilterDesignKind.Butterworth: return band == DesignBand.LowPass
                            ? new NWaves.Filters.Butterworth.LowPassFilter(f, order).Tf
                            : new NWaves.Filters.Butterworth.HighPassFilter(f, order).Tf;
                        case FilterDesignKind.ChebyshevI: return band == DesignBand.LowPass
                            ? new NWaves.Filters.ChebyshevI.LowPassFilter(f, order, DefaultRippleDecibels).Tf
                            : new NWaves.Filters.ChebyshevI.HighPassFilter(f, order, DefaultRippleDecibels).Tf;
                        case FilterDesignKind.ChebyshevII: return band == DesignBand.LowPass
                            ? new NWaves.Filters.ChebyshevII.LowPassFilter(f, order, DefaultRippleDecibels).Tf
                            : new NWaves.Filters.ChebyshevII.HighPassFilter(f, order, DefaultRippleDecibels).Tf;
                        case FilterDesignKind.Bessel: return band == DesignBand.LowPass
                            ? new NWaves.Filters.Bessel.LowPassFilter(f, order).Tf
                            : new NWaves.Filters.Bessel.HighPassFilter(f, order).Tf;
                        default: return band == DesignBand.LowPass
                            ? new NWaves.Filters.Elliptic.LowPassFilter(f, order, DefaultEllipticPassRippleDecibels, DefaultEllipticStopAttenuationDecibels).Tf
                            : new NWaves.Filters.Elliptic.HighPassFilter(f, order, DefaultEllipticPassRippleDecibels, DefaultEllipticStopAttenuationDecibels).Tf;
                    }
                }
                default:
                {
                    if (lowerFrequencyHz <= 0 || lowerFrequencyHz >= nyquist)
                        throw new ArgumentOutOfRangeException(nameof(lowerFrequencyHz), $"Lower cutoff must be within (0, {nyquist}) Hz.");
                    if (upperFrequencyHz <= lowerFrequencyHz || upperFrequencyHz >= nyquist)
                        throw new ArgumentOutOfRangeException(nameof(upperFrequencyHz), $"Upper cutoff must be within ({lowerFrequencyHz}, {nyquist}) Hz.");
                    double f1 = normalized(lowerFrequencyHz);
                    double f2 = normalized(upperFrequencyHz);
                    switch (kind)
                    {
                        case FilterDesignKind.Butterworth: return band == DesignBand.BandPass
                            ? new NWaves.Filters.Butterworth.BandPassFilter(f1, f2, order).Tf
                            : new NWaves.Filters.Butterworth.BandStopFilter(f1, f2, order).Tf;
                        case FilterDesignKind.ChebyshevI: return band == DesignBand.BandPass
                            ? new NWaves.Filters.ChebyshevI.BandPassFilter(f1, f2, order, DefaultRippleDecibels).Tf
                            : new NWaves.Filters.ChebyshevI.BandStopFilter(f1, f2, order, DefaultRippleDecibels).Tf;
                        case FilterDesignKind.ChebyshevII: return band == DesignBand.BandPass
                            ? new NWaves.Filters.ChebyshevII.BandPassFilter(f1, f2, order, DefaultRippleDecibels).Tf
                            : new NWaves.Filters.ChebyshevII.BandStopFilter(f1, f2, order, DefaultRippleDecibels).Tf;
                        case FilterDesignKind.Bessel: return band == DesignBand.BandPass
                            ? new NWaves.Filters.Bessel.BandPassFilter(f1, f2, order).Tf
                            : new NWaves.Filters.Bessel.BandStopFilter(f1, f2, order).Tf;
                        default: return band == DesignBand.BandPass
                            ? new NWaves.Filters.Elliptic.BandPassFilter(f1, f2, order, DefaultEllipticPassRippleDecibels, DefaultEllipticStopAttenuationDecibels).Tf
                            : new NWaves.Filters.Elliptic.BandStopFilter(f1, f2, order, DefaultEllipticPassRippleDecibels, DefaultEllipticStopAttenuationDecibels).Tf;
                    }
                }
            }
        }

        private static Signal<double> Filter(Signal<double> signal, NWaves.Filters.Base64.LtiFilter64 filter)
        {
            if (signal.Count == 0)
                return signal.WithSamples(Array.Empty<double>());

            var sampleArray = signal.Samples.ToArray();
            var filtered = new double[sampleArray.Length];
            for (int i = 0; i < filtered.Length; i++)
                filtered[i] = filter.Process(sampleArray[i]);
            return signal.WithSamples(filtered);
        }
    }
}