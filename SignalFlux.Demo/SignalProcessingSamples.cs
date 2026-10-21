using System;
using SignalFlux;
using SignalFlux.SignalProcessing;
using static System.Console;

namespace SignalFlux.Demo
{
    public static class SignalProcessingSamples
    {
        public static void RunSignalProcessingSample()
        {
            WriteLine("\n=== Signal Processing Demo ===\n");

            // 125 Hz tone sampled at 1 kHz for 1024 samples -> an exact FFT bin at 125.0 Hz.
            const int sampleCount = 1024;
            const double sampleRate = 1000;
            const double tone = 125;
            var samples = new double[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = Math.Sin(2 * Math.PI * tone * i / sampleRate);

            var signal = new Signal<double>(samples, sampleRate, Timestamp.UtcNow,
                unit: UnitsNet.Units.ElectricPotentialUnit.Volt);
            WriteLine($"Signal: {signal.Count} samples @ {signal.Frequency} Hz ({signal.Duration.TotalMilliseconds:F0} ms)");

            // Forward FFT -> dominant bin on the frequency axis.
            var spectrum = signal.ForwardSpectrum();
            var axis = signal.FrequencyAxis();
            int dominantBin = DominantBin(spectrum);
            WriteLine();
            WriteLine($"FFT spectrum: dominant bin #{dominantBin} -> {axis[dominantBin]:F2} Hz (expected {tone:F0} Hz)");
            WriteLine($"  Magnitude at {tone:F0} Hz: {spectrum[dominantBin].Magnitude:F1} (expected {sampleCount / 2.0:F1})");

            var power = signal.PowerSpectrum();
            WriteLine($"Power spectrum peak: {power[dominantBin]:F0} (expected {sampleCount * sampleCount / 4.0:F0})");

            var psd = signal.PowerSpectralDensity();
            WriteLine($"PSD (one-sided, {psd.Length} bins): peak {psd[dominantBin]:F1} at {axis[dominantBin]:F2} Hz");

            // Inverse FFT round-trip.
            var reconstructed = spectrum.InverseSpectrum(sampleRate, signal.StartTime, signal.Unit);
            WriteLine($"Inverse FFT round-trip: max sample error {MaxAbsError(signal, reconstructed):E1}");
            WriteLine();

            // Peak detection: one peak per cycle of the tone.
            var peaks = signal.FindPeaksDetailed(threshold: 0.5);
            WriteLine($"Peak detection: {peaks.Length} peaks (threshold 0.5) — one per cycle of the {tone:F0} Hz tone");
            if (peaks.Length > 0)
                WriteLine($"  First peak: sample {peaks[0].Index} @ {peaks[0].Value:F3} V");
            WriteLine();

            // Envelope: recover a 2 Hz amplitude modulation riding on a 100 Hz carrier.
            const int amCount = 1000;
            var am = new double[amCount];
            for (int i = 0; i < amCount; i++)
            {
                double t = (double)i / sampleRate;
                am[i] = (1 + 0.8 * Math.Sin(2 * Math.PI * 2 * t)) * Math.Sin(2 * Math.PI * 100 * t);
            }

            var amSignal = new Signal<double>(am, sampleRate, Timestamp.UtcNow,
                unit: UnitsNet.Units.ElectricPotentialUnit.Volt);
            var envelope = amSignal.Envelope();
            double envMin = double.MaxValue, envMax = double.MinValue;
            var envSpan = envelope.Samples.Span;
            for (int i = 50; i < envSpan.Length - 50; i++) // skip Hilbert boundary artifacts
            {
                if (envSpan[i] < envMin) envMin = envSpan[i];
                if (envSpan[i] > envMax) envMax = envSpan[i];
            }

            WriteLine();
            WriteLine($"Envelope (2 Hz AM on 100 Hz carrier, Hilbert): [{envMin:F2}, {envMax:F2}] V");
            WriteLine($"  Expected: amplitude modulation spans (1 ± 0.8) V");
            WriteLine();
            WriteLine("--- filters, Welch PSD, wavelets ---");
            WriteLine();

            // Low-pass filter: 50 Hz tone contaminated with high-frequency noise. The causal Butterworth
            // has a group delay, so gauge the cleanup via a high-pass residue instead of a sample-aligned
            // comparison with the ideal tone.
            var rng = new Random(11);
            var noisy = new double[2048];
            for (int i = 0; i < noisy.Length; i++)
                noisy[i] = Math.Sin(2 * Math.PI * 50 * i / sampleRate) + (rng.NextDouble() - 0.5);
            var noisySignal = new Signal<double>(noisy, sampleRate, Timestamp.UtcNow,
                unit: UnitsNet.Units.ElectricPotentialUnit.Volt);
            var filtered = noisySignal.LowPass(cutoffFrequencyHz: 80, order: 4);
            double noiseHf = Rms(noisySignal.HighPass(cutoffFrequencyHz: 200, order: 8));
            double filteredHf = Rms(filtered.HighPass(cutoffFrequencyHz: 200, order: 8));
            WriteLine($"LowPass(80 Hz, order 4): high-frequency residue {noiseHf:F3} V -> {filteredHf:F3} V");

            WriteLine();
            var denoised = noisySignal.Denoise(windowSize: 15);
            WriteLine($"Savitzky-Golay Denoise(window 15): noise {ToneResidualRms(noisySignal, sampleRate, 50):F4} V -> {ToneResidualRms(denoised, sampleRate, 50):F4} V");

            // Welch PSD of the AM carrier -> peak near the 100 Hz carrier.
            var welch = amSignal.WelchPsd(segmentSize: 256);
            int welchPeak = 0;
            for (int i = 1; i < welch.Power.Length; i++)
                if (welch.Power[i] > welch.Power[welchPeak])
                    welchPeak = i;
            WriteLine($"\nWelch PSD (256-sample segments): peak at {welch.Frequencies[welchPeak]:F1} Hz (expected near 100 Hz)");
            WriteLine($"  {welch.Power.Length} one-sided bins; out-of-band fraction {OutOfBand(amSignal.WelchPsd(segmentSize: 256), 100):F4}");

            // Wavelet denoise of the same noisy tone (zero-phase, single level).
            var waveletCleaned = noisySignal.WaveletDenoise(threshold: 0.5, soft: true, WaveletKind.Daubechies, taps: 4);
            WriteLine($"\nWavelet denoise (db4, soft threshold 0.5): noise {ToneResidualRms(noisySignal, sampleRate, 50):F4} V -> {ToneResidualRms(waveletCleaned, sampleRate, 50):F4} V");
        }

        private static double ToneResidualRms(Signal<double> signal, double sampleRate, double toneHz)
        {
            var values = signal.Samples.ToArray();
            double sumSq = 0;
            for (int i = values.Length / 2; i < values.Length; i++)
            {
                double residual = values[i] - Math.Sin(2 * Math.PI * toneHz * i / sampleRate);
                sumSq += residual * residual;
            }
            return Math.Sqrt(sumSq / (values.Length - values.Length / 2));
        }

        private static double Rms(Signal<double> signal)
        {
            var values = signal.Samples.ToArray();
            double sumSq = 0;
            for (int i = values.Length / 2; i < values.Length; i++)
                sumSq += values[i] * values[i];
            return Math.Sqrt(sumSq / (values.Length - values.Length / 2));
        }

        private static double OutOfBand(WelchPsdResult psd, double toneHz)
        {
            double total = 0, outside = 0;
            for (int i = 0; i < psd.Power.Length; i++)
            {
                total += psd.Power[i];
                if (Math.Abs(psd.Frequencies[i] - toneHz) > 5)
                    outside += psd.Power[i];
            }
            return total > 0 ? outside / total : 0;
        }

        private static int DominantBin(System.Numerics.Complex[] spectrum)
        {
            int best = 0;
            for (int i = 1; i < spectrum.Length; i++)
                if (spectrum[i].Magnitude > spectrum[best].Magnitude)
                    best = i;
            return best;
        }

        private static double MaxAbsError(Signal<double> a, Signal<double> b)
        {
            var x = a.Samples.Span;
            var y = b.Samples.Span;
            double max = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double err = Math.Abs(x[i] - y[i]);
                if (err > max) max = err;
            }
            return max;
        }
    }
}