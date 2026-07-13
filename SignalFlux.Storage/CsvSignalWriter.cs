using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnitsNet;

namespace SignalFlux.Storage
{
    /// <summary>Streaming CSV writer for signals and experiment metadata.</summary>
    public sealed class CsvSignalWriter : IAsyncDisposable
    {
        private readonly StreamWriter _writer;
        private bool _headerWritten;

        /// <summary>Creates a CSV writer targeting the specified file path.</summary>
        /// <param name="filePath">Path to the CSV file.</param>
        /// <param name="append">If true, appends to an existing file.</param>
        public CsvSignalWriter(string filePath, bool append = false)
        {
            _writer = new StreamWriter(filePath, append);
        }

        /// <summary>Creates a CSV writer targeting the specified stream.</summary>
        /// <param name="stream">The stream to write to.</param>
        public CsvSignalWriter(Stream stream)
        {
            _writer = new StreamWriter(stream);
        }

        /// <summary>Writes a signal's samples to the CSV file. The header is written on the first call.</summary>
        /// <param name="signal">The signal to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WriteSignalAsync(Signal<double> signal, CancellationToken cancellationToken = default)
        {
            if (!_headerWritten)
            {
                await _writer.WriteLineAsync("Time,Value,Unit,Quality,Source").ConfigureAwait(false);
                _headerWritten = true;
            }

            var samples = signal.Samples.ToArray();
            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double timeOffset = i / signal.Frequency;
                var timestamp = signal.StartTime + TimeSpan.FromSeconds(timeOffset);
                await _writer.WriteLineAsync(
                    $"{timestamp.DateTime:O},{samples[i].ToString(CultureInfo.InvariantCulture)},{signal.Unit},{(int)signal.Quality},{signal.Source}")
                    .ConfigureAwait(false);
            }

            await _writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>Writes experiment metadata as CSV comments.</summary>
        /// <param name="experiment">The experiment to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WriteExperimentAsync(Experiment experiment, CancellationToken cancellationToken = default)
        {
            await _writer.WriteLineAsync($"# Experiment: {experiment.Id}").ConfigureAwait(false);
            await _writer.WriteLineAsync($"# Start: {experiment.Start.DateTime:O}").ConfigureAwait(false);
            await _writer.WriteLineAsync($"# Operator: {experiment.Operator}").ConfigureAwait(false);
            await _writer.WriteLineAsync($"# Equipment: {string.Join(", ", experiment.Equipment)}").ConfigureAwait(false);
            await _writer.WriteLineAsync().ConfigureAwait(false);
        }

        /// <summary>Disposes the writer, flushing remaining data.</summary>
        public ValueTask DisposeAsync()
        {
            _writer.Dispose();
            return default;
        }
    }
}
