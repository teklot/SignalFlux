using System;
using NWaves.Transforms.Wavelets;
using NWaves.Utils;

namespace SignalFlux.SignalProcessing
{
    /// <summary>Identifies the wavelet family used for the fast wavelet transform.</summary>
    public enum WaveletKind
    {
        /// <summary>The Haar (db1) wavelet.</summary>
        Haar,
        /// <summary>Daubechies orthogonal wavelets (taps 2-20).</summary>
        Daubechies,
        /// <summary>Coiflet wavelets (taps 1-5).</summary>
        Coiflet,
        /// <summary>Symlet least-asymmetric wavelets (taps 2-20).</summary>
        Symlet
    }

    /// <summary>
    /// Fast wavelet transforms for <see cref="Signal{T}"/>, delegated to NWaves. The transform engine is
    /// single-precision in NWaves, so coefficients round-trip through the float seam; reconstruction error is
    /// on the order of float precision. Filter lengths are kept modest so input signals must have at least
    /// 16 samples and an even length.
    /// </summary>
    public static class WaveletExtensions
    {
        /// <summary>
        /// Computes the fast wavelet transform of the signal, returning the coefficient array in the FWT
        /// multiresolution layout (scaling coefficients followed by detail coefficients).
        /// </summary>
        /// <param name="signal">The source signal (at least 16 samples, even length).</param>
        /// <param name="family">Wavelet family.</param>
        /// <param name="taps">Wavelet filter taps (family-dependent ranges).</param>
        /// <param name="levels">Number of decomposition levels (default 1; pass 0 for the maximum).</param>
        /// <returns>FWT coefficients, same length as the signal.</returns>
        /// <exception cref="ArgumentException">Thrown when the signal length is invalid or <paramref name="levels"/> exceeds the maximum for the length.</exception>
        public static double[] WaveletTransform(
            this Signal<double> signal,
            WaveletKind family = WaveletKind.Daubechies,
            int taps = 4,
            int levels = 1)
        {
            if (signal.Count < 16)
                throw new ArgumentException("Wavelet transform requires at least 16 samples.", nameof(signal));
            if (signal.Count % 2 != 0)
                throw new ArgumentException("Wavelet transform requires an even sample count.", nameof(signal));

            var input = signal.Samples.ToArray().ToFloats();
            var wavelet = CreateWavelet(family, taps);
            var fwt = new Fwt(input.Length, wavelet);
            var output = new float[input.Length];
            if (levels <= 0)
                fwt.Direct(input, output);
            else
                fwt.Direct(input, output, levels);
            return output.ToDoubles();
        }

        /// <summary>Reconstructs a signal from FWT coefficients produced by <see cref="WaveletTransform"/>.</summary>
        /// <param name="coefficients">The FWT coefficient array.</param>
        /// <param name="frequency">The sampling frequency in Hz for the reconstructed signal.</param>
        /// <param name="startTime">The UTC time of the first sample.</param>
        /// <param name="unit">The unit of the reconstructed samples.</param>
        /// <param name="family">Wavelet family used for the forward transform.</param>
        /// <param name="taps">Wavelet filter taps used for the forward transform.</param>
        /// <param name="levels">Number of decomposition levels used for the forward transform (default 1; pass 0 for the maximum).</param>
        /// <returns>A signal whose samples approximate the original forward-transform input.</returns>
        /// <exception cref="ArgumentException">Thrown when the coefficient array is invalid for the wavelet.</exception>
        public static Signal<double> WaveletReconstruct(
            this double[] coefficients,
            double frequency,
            Timestamp startTime,
            Enum? unit = null,
            WaveletKind family = WaveletKind.Daubechies,
            int taps = 4,
            int levels = 1)
        {
            if (coefficients == null)
                throw new ArgumentNullException(nameof(coefficients));
            if (coefficients.Length < 16 || coefficients.Length % 2 != 0)
                throw new ArgumentException("Coefficient array must have at least 16 samples and an even length.", nameof(coefficients));

            var wavelet = CreateWavelet(family, taps);
            var fwt = new Fwt(coefficients.Length, wavelet);
            var input = coefficients.ToFloats();
            var output = new float[input.Length];
            if (levels <= 0)
                fwt.Inverse(input, output);
            else
                fwt.Inverse(input, output, levels);
            return new Signal<double>(output.ToDoubles(), frequency, startTime, unit);
        }

        /// <summary>
        /// Denoises the signal by hard- or soft-thresholding the detail coefficients of a single-level FWT,
        /// then reconstructing. The approximation half is preserved verbatim.
        /// </summary>
        /// <param name="signal">The source signal (at least 16 samples, even length).</param>
        /// <param name="threshold">Coefficients with magnitude below this are zeroed (hard) or shrunk (soft).</param>
        /// <param name="soft">When true, apply soft (shrinkage) thresholding; otherwise hard thresholding.</param>
        /// <param name="family">Wavelet family.</param>
        /// <param name="taps">Wavelet filter taps.</param>
        /// <returns>A denoised signal of the same length and sampling metadata as the source.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when the signal length is invalid.</exception>
        public static Signal<double> WaveletDenoise(
            this Signal<double> signal,
            double threshold,
            bool soft = false,
            WaveletKind family = WaveletKind.Daubechies,
            int taps = 4)
        {
            if (threshold < 0)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be non-negative.");
            if (signal.Count < 16)
                throw new ArgumentException("Wavelet transform requires at least 16 samples.", nameof(signal));
            if (signal.Count % 2 != 0)
                throw new ArgumentException("Wavelet transform requires an even sample count.", nameof(signal));

            var input = signal.Samples.ToArray().ToFloats();
            var wavelet = CreateWavelet(family, taps);
            var fwt = new Fwt(input.Length, wavelet);
            var output = new float[input.Length];
            fwt.Direct(input, output, 1);

            int half = output.Length / 2;
            for (int i = half; i < output.Length; i++)
            {
                float c = Math.Abs(output[i]);
                if (c <= threshold)
                {
                    output[i] = 0f;
                }
                else if (soft)
                {
                    float sign = output[i] >= 0 ? 1f : -1f;
                    output[i] = sign * (c - (float)threshold);
                }
            }

            var reconstructed = new float[output.Length];
            fwt.Inverse(output, reconstructed, 1);
            return signal.WithSamples(reconstructed.ToDoubles());
        }

        private static Wavelet CreateWavelet(WaveletKind family, int taps)
        {
            if (taps < 1)
                throw new ArgumentOutOfRangeException(nameof(taps), "Wavelet taps must be positive.");
            WaveletFamily nwaves;
            switch (family)
            {
                case WaveletKind.Haar: nwaves = WaveletFamily.Haar; break;
                case WaveletKind.Daubechies: nwaves = WaveletFamily.Daubechies; break;
                case WaveletKind.Coiflet: nwaves = WaveletFamily.Coiflet; break;
                default: nwaves = WaveletFamily.Symlet; break;
            }
            return new Wavelet(nwaves, taps);
        }
    }
}