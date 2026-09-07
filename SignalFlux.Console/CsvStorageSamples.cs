using System;
using System.IO;
using System.Threading.Tasks;
using SignalFlux;
using SignalFlux.Storage;
using static System.Console;

namespace SignalFlux.Console
{
    public static class CsvStorageSamples
    {
        public static async Task RunRoundTripAsync(Signal<double> signal, Experiment experiment)
        {
            WriteLine("\n=== CSV Storage Demo ===");
            WriteLine();

            var csvPath = Path.GetTempFileName() + ".csv";

            try
            {
                await using (var writer = new CsvSignalWriter(csvPath))
                {
                    await writer.WriteExperimentAsync(experiment);
                    await writer.WriteSignalAsync(signal);
                }
                WriteLine($"Written to: {csvPath}");

                await using (var reader = new CsvSignalReader(csvPath))
                {
                    var signals = await reader.ReadAllSignalsAsync();
                    if (signals.Count > 0)
                    {
                        var read = signals[0];
                        WriteLine($"Read back: {read.Count} samples @ {read.Frequency:F1} Hz, unit: {read.Unit}, source: {read.Source}");
                        WriteLine($"Round-trip integrity: {Validate(read, signal)}");
                    }
                }
            }
            finally
            {
                File.Delete(csvPath);
                WriteLine("Cleanup done.");
            }
        }

        private static bool Validate(Signal<double> read, Signal<double> original)
        {
            if (read.Count != original.Count)
                return false;

            var a = read.Samples.Span;
            var b = original.Samples.Span;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i] - b[i]) > 1e-12)
                    return false;

            return true;
        }
    }
}