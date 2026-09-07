using System;
using SignalFlux;
using SignalFlux.Generators;
using SignalFlux.TimeSeries;
using static System.Console;

namespace SignalFlux.Console
{
    /// <summary>
    /// SignalFlux console demos entry point.
    /// With no arguments all demos run in sequence; use --demo &lt;name&gt; to run a single demo
    /// and --list to enumerate the available demo names.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var options = CliOptions.Parse(args);

            if (options.ShowHelp)
            {
                CliOptions.PrintHelp();
                return 0;
            }

            if (options.ListDemos)
            {
                CliOptions.PrintDemos();
                return 0;
            }

            if (args.Length > 0)
            {
                WriteLine("SignalFlux — Engineering Computing for .NET");
                WriteLine(new string('-', 50));

                var demos = options.Demos.Count > 0 ? options.Demos : CliOptions.AllDemos;
                await RunDemos(demos);

                WriteLine("\nSignalFlux is ready. Build something great.");
                return 0;
            }

            await RunMenu();
            return 0;
        }

        private static async Task RunMenu()
        {
            while (true)
            {
                TryClearScreen();
                WriteLine("=== SignalFlux Console Menu ===");
                WriteLine();
                WriteLine("  1. Run all demos");
                WriteLine("  2. Core domain model");
                WriteLine("  3. CSV storage round-trip");
                WriteLine("  4. Live acquisition");
                WriteLine("  5. Signal processing");
                WriteLine("  6. Protocol adapters");
                WriteLine("  7. Visualization (ScottPlot)");
                WriteLine("  8. OPC UA client");
                WriteLine("  0. Exit");
                WriteLine();
                Write("Choose an option: ");

                string? input = ReadLine();
                if (!int.TryParse(input?.Trim(), out int choice))
                {
                    WriteLine("Please enter a valid number.");
                    WriteLine("Press Enter to continue...");
                    ReadLine();
                    continue;
                }

                if (choice == 0)
                    return;

                if (choice is >= 1 and <= 8)
                {
                    await RunMenuChoice(choice);
                    WriteLine();
                    WriteLine("Press Enter to return to the menu...");
                    ReadLine();
                }
                else
                {
                    WriteLine("Unknown option.");
                    WriteLine("Press Enter to continue...");
                    ReadLine();
                }
            }
        }

        private static async Task RunMenuChoice(int choice)
        {
            switch (choice)
            {
                case 1: await RunDemos(CliOptions.AllDemos); break;
                case 2: await RunDemos(new[] { "core" }); break;
                case 3: await RunDemos(new[] { "csv" }); break;
                case 4: await RunDemos(new[] { "acquisition" }); break;
                case 5: await RunDemos(new[] { "signalprocessing" }); break;
                case 6: await RunDemos(new[] { "protocols" }); break;
                case 7: await RunDemos(new[] { "visualization" }); break;
                case 8: await RunDemos(new[] { "opcua" }); break;
            }
        }

        private static void TryClearScreen()
        {
            try
            {
                System.Console.Clear();
            }
            catch (IOException)
            {
                // No attached console (e.g. piped input); clearing is a no-op.
            }
        }

        private static async Task RunDemos(IReadOnlyList<string> demos)
        {
            foreach (var name in demos)
            {
                switch (name)
                {
                    case "core":
                        RunCoreDemo();
                        break;
                    case "csv":
                        await RunCsvDemo();
                        break;
                    case "acquisition":
                        try
                        {
                            await AcquisitionSample.RunAsync();
                        }
                        catch (Exception ex)
                        {
                            WriteLine($"Acquisition demo skipped: {ex.Message}");
                        }
                        break;
                    case "signalprocessing":
                        try
                        {
                            SignalProcessingSamples.RunSignalProcessingSample();
                        }
                        catch (Exception ex)
                        {
                            WriteLine($"Signal processing demo skipped: {ex.Message}");
                        }
                        break;
                    case "protocols":
                        try
                        {
                            ProtocolSamples.RunModbusSample();
                            ProtocolSamples.RunMavlinkSample();
                            ProtocolSamples.RunNmeaSample();
                            await ProtocolSamples.RunCanSample();
                            ProtocolSamples.RunArinc429Sample();
                        }
                        catch (Exception ex)
                        {
                            WriteLine($"Protocol adapters demo skipped: {ex.Message}");
                        }
                        break;
                    case "visualization":
                        try
                        {
                            VisualizationSamples.RunScottPlotSample();
                        }
                        catch (Exception ex)
                        {
                            WriteLine($"Visualization demo skipped: {ex.Message}");
                        }
                        break;
                    case "opcua":
                        await RunOpcUaDemo();
                        break;
                    default:
                        WriteLine($"Unknown demo '{name}'. Run with --help for the list.");
                        break;
                }
            }
        }

        private static void RunCoreDemo()
        {
            var (signal, _) = BuildCoreContext();

            WriteLine($"Generated signal: {signal.Count} samples @ {signal.Frequency} Hz");
            WriteLine($"Duration: {signal.Duration.TotalSeconds:F3}s");
            WriteLine($"Unit: {signal.Unit}");
            WriteLine($"Start: {signal.StartTime.DateTime:O}");
            WriteLine($"Quality: {signal.Quality}");

            var stats = signal.Statistics();
            WriteLine("\nStatistics:");
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
        }

        private static async Task RunCsvDemo()
        {
            var (signal, experiment) = BuildCoreContext();
            try
            {
                await CsvStorageSamples.RunRoundTripAsync(signal, experiment);
            }
            catch (Exception ex)
            {
                WriteLine($"CSV storage demo skipped: {ex.Message}");
            }
        }

        private static async Task RunOpcUaDemo()
        {
            try
            {
                await OpcUaSamples.RunSampleAsync();
            }
            catch (Exception ex)
            {
                WriteLine($"OPC UA demo skipped: {ex.Message}");
            }
        }

        private static (Signal<double> Signal, Experiment Experiment) BuildCoreContext()
        {
            var sine = new SineGenerator(frequency: 100, amplitude: 5.0);
            var signal = sine.GenerateSignal(1000);

            var experiment = new Experiment(
                id: "demo-001",
                signals: new Dictionary<string, object> { { "Voltage", signal } },
                @operator: "TekLot",
                start: Timestamp.UtcNow);

            return (signal, experiment);
        }
    }
}