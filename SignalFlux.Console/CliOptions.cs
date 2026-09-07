using System;
using System.Collections.Generic;

namespace SignalFlux.Console
{
    /// <summary>
    /// Parsed command-line options for the SignalFlux console demos.
    /// </summary>
    internal sealed class CliOptions
    {
        /// <summary>The canonical demo names in default run order.</summary>
        public static readonly IReadOnlyList<string> AllDemos = new[]
        {
            "core", "csv", "acquisition", "signalprocessing", "protocols", "visualization", "opcua"
        };

        /// <summary>Selected demo names when running non-interactively.</summary>
        public List<string> Demos { get; } = new();

        /// <summary>True when usage help was requested.</summary>
        public bool ShowHelp { get; private set; }

        /// <summary>True when the demo list was requested.</summary>
        public bool ListDemos { get; private set; }

        private CliOptions() { }

        /// <summary>
        /// Parses command-line arguments into a <see cref="CliOptions"/>.
        /// Unknown dash-prefixed arguments are ignored; bare words are treated as demo names.
        /// </summary>
        /// <param name="args">Raw command-line arguments.</param>
        /// <returns>The parsed options.</returns>
        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--list":
                        options.ListDemos = true;
                        break;
                    case "--demo":
                        if (i + 1 < args.Length)
                        {
                            foreach (var name in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                options.Demos.Add(name.ToLowerInvariant());
                        }
                        break;
                    default:
                        if (args[i].StartsWith("-", StringComparison.Ordinal))
                            break; // unknown flag
                        options.Demos.Add(args[i].ToLowerInvariant());
                        break;
                }
            }

            return options;
        }

        /// <summary>Prints the available demos to the console.</summary>
        public static void PrintDemos()
        {
            System.Console.WriteLine("Available demos:");
            foreach (var name in AllDemos)
                System.Console.WriteLine($"  {name}");
        }

        /// <summary>Prints usage help to the console.</summary>
        public static void PrintHelp()
        {
            System.Console.WriteLine("SignalFlux console demos");
            System.Console.WriteLine();
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine("  SignalFlux.Console [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Demos:");
            System.Console.WriteLine("  core               Core domain model walkthrough (Signal, statistics, Experiment, Session)");
            System.Console.WriteLine("  csv                CSV storage round-trip");
            System.Console.WriteLine("  acquisition        Live acquisition pipeline (simulated sensor -> TCP -> Signal -> CSV + SQLite)");
            System.Console.WriteLine("  signalprocessing   FFT / spectrum / peaks / PSD / envelope");
            System.Console.WriteLine("  protocols          Modbus, MAVLink, NMEA, CAN (DBC), ARINC 429");
            System.Console.WriteLine("  visualization      ScottPlot render");
            System.Console.WriteLine("  opcua              OPC UA client demo");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  (no arguments)       Show the interactive demo menu.");
            System.Console.WriteLine("  --demo <name>        Run a single demo. Repeatable and comma-separated (--demo core,csv).");
            System.Console.WriteLine("  --list               List available demos.");
            System.Console.WriteLine("  --help, -h           Show this help.");
        }
    }
}