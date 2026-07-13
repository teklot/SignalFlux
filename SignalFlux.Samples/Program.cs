using System;
using SignalFlux;
using SignalFlux.Generators;
using SignalFlux.TimeSeries;
using SignalFlux.Storage;
using SignalFlux.Samples;
using static System.Console;

WriteLine("SignalFlux — Engineering Computing for .NET");
WriteLine(new string('-', 50));

var sine = new SineGenerator(frequency: 100, amplitude: 5.0);
var signal = sine.GenerateSignal(1000);

WriteLine($"Generated signal: {signal.Count} samples @ {signal.Frequency} Hz");
WriteLine($"Duration: {signal.Duration.TotalSeconds:F3}s");
WriteLine($"Unit: {signal.Unit}");
WriteLine($"Start: {signal.StartTime.DateTime:O}");
WriteLine($"Quality: {signal.Quality}");

var stats = signal.Statistics();
WriteLine($"\nStatistics:");
WriteLine($"  Mean: {stats.Mean:F4}");
WriteLine($"  StdDev: {stats.StandardDeviation:F4}");
WriteLine($"  Min: {stats.Minimum:F4}");
WriteLine($"  Max: {stats.Maximum:F4}");
WriteLine($"  Range: {stats.Range:F4}");

var resampled = signal.Resample(50);
WriteLine($"\nResampled: {resampled.Count} samples @ {resampled.Frequency} Hz");

var normalized = signal.Normalize();
var normStats = normalized.Statistics();
WriteLine($"\nNormalized — Min: {normStats.Minimum:F4}, Max: {normStats.Maximum:F4}");

var noise = new NoiseGenerator(frequency: 100, amplitude: 2.0, seed: 42);
var noiseSignal = noise.GenerateSignal(1000);

var merged = signal.Merge(noiseSignal, MergeMethod.Average);
WriteLine($"\nMerged (signal + noise): {merged.Count} samples");

var chunk = signal.Window(100, 200);
WriteLine($"Windowed (100..300): {chunk.Count} samples");

var aligned = signal.Align(noiseSignal);
WriteLine($"Aligned: {aligned.Count} samples with cross-references");

var experiment = new Experiment(
    id: "demo-001",
    signals: new Dictionary<string, object> { { "Voltage", signal } },
    @operator: "TekLot",
    start: Timestamp.UtcNow);

WriteLine($"\nExperiment: {experiment}");

var session = new Session(
    id: "session-001",
    experiments: new[] { experiment },
    canReplay: true);

WriteLine($"Session: {session}");

// CSV round-trip demo
var csvPath = Path.GetTempFileName() + ".csv";
WriteLine($"\n--- CSV Storage Demo ---");
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
        WriteLine($"Read back: {read.Count} samples @ {read.Frequency:F1} Hz, unit: {read.Unit}");
    }
}

File.Delete(csvPath);
WriteLine("Cleanup done.");

WriteLine("\n--- Live Acquisition Demo ---");
await AcquisitionSample.RunAsync();

WriteLine("\nSignalFlux is ready. Build something great.");
