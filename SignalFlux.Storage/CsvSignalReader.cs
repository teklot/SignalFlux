using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnitsNet;

namespace SignalFlux.Storage
{
    /// <summary>CSV reader for signals, supporting both in-memory and streaming reads.</summary>
#if NET10_0
    public sealed class CsvSignalReader : IAsyncDisposable
#else
    public sealed class CsvSignalReader : IDisposable
#endif
    {
        private readonly StreamReader _reader;
        private bool _headerSkipped;

        private static readonly Dictionary<string, Enum> UnitNameLookup = BuildUnitNameLookup();

        private static Dictionary<string, Enum> BuildUnitNameLookup()
        {
            var lookup = new Dictionary<string, Enum>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in Quantity.Infos)
            {
                foreach (var value in Enum.GetValues(info.UnitType))
                {
                    var name = value.ToString();
                    if (!lookup.ContainsKey(name))
                        lookup[name] = (Enum)value;
                }
            }
            return lookup;
        }

        private static Enum ParseUnit(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (UnitNameLookup.TryGetValue(s, out var unit))
                return unit;
            foreach (var info in Quantity.Infos)
            {
                if (UnitParser.Default.TryParse(s, info.UnitType, out var result))
                    return (Enum)result;
            }
            return null;
        }

        /// <summary>Creates a CSV reader for the specified file.</summary>
        /// <param name="filePath">Path to the CSV file.</param>
        public CsvSignalReader(string filePath)
        {
            _reader = new StreamReader(filePath);
        }

        /// <summary>Creates a CSV reader for the specified stream.</summary>
        /// <param name="stream">The stream to read from.</param>
        public CsvSignalReader(Stream stream)
        {
            _reader = new StreamReader(stream);
        }

        /// <summary>Reads all signals from the CSV into memory.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of signals parsed from the CSV.</returns>
        public async Task<List<Signal<double>>> ReadAllSignalsAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<Signal<double>>();
            var samples = new List<double>();
            Enum unit = null;
            string source = "";
            Timestamp startTime = Timestamp.Zero;
            Timestamp lastTimestamp = Timestamp.Zero;
            bool hasData = false;

            string line;
            while ((line = await _reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("#"))
                    continue;

                if (!_headerSkipped)
                {
                    _headerSkipped = true;
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 2)
                    continue;

                if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    var ts = Timestamp.FromDateTime(dt);
                    if (!hasData)
                    {
                        startTime = ts;
                        hasData = true;
                    }
                    lastTimestamp = ts;

                    if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        samples.Add(value);
                    }

                    if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                        unit = ParseUnit(parts[2]);
                    if (parts.Length >= 5 && !string.IsNullOrEmpty(parts[4]))
                        source = parts[4];
                }
            }

            if (samples.Count > 0)
            {
                double totalSeconds = (lastTimestamp - startTime).TotalSeconds;
                double frequency = samples.Count / Math.Max(totalSeconds, 1e-6);
                result.Add(new Signal<double>(samples.ToArray(), frequency, startTime, unit, source: source));
            }

            return result;
        }

#if NET10_0
        /// <summary>Streams signal chunks of the specified size from the CSV.</summary>
        /// <param name="samplesPerChunk">Number of samples per yielded chunk.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async IAsyncEnumerable<Signal<double>> ReadStreamingAsync(
            int samplesPerChunk,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var samples = new List<double>();
            Enum unit = null;
            string source = "";
            Timestamp startTime = Timestamp.Zero;
            Timestamp lastTimestamp = Timestamp.Zero;
            bool hasData = false;

            string line;
            while ((line = await _reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("#"))
                    continue;

                if (!_headerSkipped)
                {
                    _headerSkipped = true;
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 2)
                    continue;

                if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    var ts = Timestamp.FromDateTime(dt);
                    if (!hasData)
                    {
                        startTime = ts;
                        hasData = true;
                    }
                    lastTimestamp = ts;

                    if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        samples.Add(value);
                    }

                    if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                        unit = ParseUnit(parts[2]);
                    if (parts.Length >= 5 && !string.IsNullOrEmpty(parts[4]))
                        source = parts[4];
                }

                if (samples.Count >= samplesPerChunk)
                {
                    double totalSeconds = (lastTimestamp - startTime).TotalSeconds;
                    double frequency = samplesPerChunk / Math.Max(totalSeconds, 1e-6);
                    yield return new Signal<double>(samples.ToArray(), frequency, startTime, unit, source: source);
                    samples.Clear();
                    hasData = false;
                }
            }

            if (samples.Count > 0)
            {
                double totalSeconds = (lastTimestamp - startTime).TotalSeconds;
                double frequency = samples.Count / Math.Max(totalSeconds, 1e-6);
                yield return new Signal<double>(samples.ToArray(), frequency, startTime, unit, source: source);
            }
        }
#endif

#if NET10_0
        /// <summary>Disposes the reader.</summary>
        public ValueTask DisposeAsync()
        {
            _reader.Dispose();
            return default;
        }
#else
        /// <summary>Disposes the reader.</summary>
        public void Dispose()
        {
            _reader.Dispose();
        }
#endif
    }
}
