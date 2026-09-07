using System;
using SignalFlux;
using SignalFlux.SignalProcessing;
using static System.Console;

namespace SignalFlux.Console
{
    public static class SignalProcessingSamples
    {
        public static void RunSignalProcessingSample()
        {
            WriteLine("=== Signal Processing Demo ===");
            WriteLine();

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
            var envelope = amSignal.Envelope(windowSize: 101);
            double envMin = double.MaxValue, envMax = double.MinValue;
            var envSpan = envelope.Samples.Span;
            for (int i = 0; i < envSpan.Length; i++)
            {
                if (envSpan[i] < envMin) envMin = envSpan[i];
                if (envSpan[i] > envMax) envMax = envSpan[i];
            }

            WriteLine($"Envelope (2 Hz AM on 100 Hz carrier, window 101): min {envMin:F2} V, max {envMax:F2} V");
            WriteLine($"  Expected: amplitude modulation spans (1 ± 0.8) V");
            WriteLine();
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