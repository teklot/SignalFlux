using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace SignalFlux.Storage
{
    /// <summary>Parquet-backed signal store providing read/write access to persisted signals.</summary>
    public sealed class ParquetSignalStore : ISignalStore
    {
        private readonly string _directoryPath;

        /// <summary>Creates a store backed by the specified directory. The directory is created if it does not exist.</summary>
        /// <param name="directoryPath">Directory path for storing Parquet and metadata files.</param>
        public ParquetSignalStore(string directoryPath)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Directory.CreateDirectory(directoryPath);
        }

        /// <summary>Writes a signal to a Parquet file, creating a companion metadata file.</summary>
        /// <param name="signal">The signal to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WriteSignalAsync<T>(Signal<T> signal, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(signal.Source);

            var samples = signal.Samples.ToArray();
            var timestamps = new DateTime[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                timestamps[i] = (signal.StartTime + TimeSpan.FromSeconds(i / signal.Frequency)).DateTime;

            var schema = new ParquetSchema(
                new DataField<DateTime>("timestamp"),
                new DataField<double>("value"),
                new DataField<int>("quality"));

            using var fs = File.Create(filePath);
            using var writer = await ParquetWriter.CreateAsync(schema, fs, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            using var rowGroup = writer.CreateRowGroup();
            await rowGroup.WriteColumnAsync(new DataColumn(schema.DataFields[0], timestamps), cancellationToken)
                .ConfigureAwait(false);

            var doubleValues = samples.Select(s => Convert.ToDouble(s)).ToArray();
            await rowGroup.WriteColumnAsync(new DataColumn(schema.DataFields[1], doubleValues), cancellationToken)
                .ConfigureAwait(false);

            var qualityValues = Enumerable.Repeat((int)signal.Quality, samples.Length).ToArray();
            await rowGroup.WriteColumnAsync(new DataColumn(schema.DataFields[2], qualityValues), cancellationToken)
                .ConfigureAwait(false);

            await WriteMetadataFile(signal.Source, signal, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads a signal from the store by its source identifier.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="KeyNotFoundException">Thrown when no signal with the given source exists.</exception>
        public async Task<Signal<T>> ReadSignalAsync<T>(string source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(source);

            if (!File.Exists(filePath))
                throw new KeyNotFoundException($"Signal with source '{source}' not found.");

            var metadata = await ReadMetadataFile(source, cancellationToken).ConfigureAwait(false);
            if (metadata == null)
                throw new InvalidOperationException($"Metadata file for signal '{source}' is missing or corrupt.");

            using var fs = File.OpenRead(filePath);
            using var reader = await ParquetReader.CreateAsync(fs, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var timestamps = new List<DateTime>();
            var values = new List<double>();
            for (int rg = 0; rg < reader.RowGroupCount; rg++)
            {
                using var rowGroup = reader.OpenRowGroupReader(rg);
                var tsCol = await rowGroup.ReadColumnAsync(reader.Schema.DataFields[0], cancellationToken)
                    .ConfigureAwait(false);
                var valCol = await rowGroup.ReadColumnAsync(reader.Schema.DataFields[1], cancellationToken)
                    .ConfigureAwait(false);

                timestamps.AddRange((DateTime[])tsCol.Data);
                values.AddRange((double[])valCol.Data);
            }

            if (values.Count == 0)
                throw new InvalidOperationException("Signal is empty.");

            var startTime = Timestamp.FromDateTime(timestamps[0]);
            double totalSeconds = (timestamps[timestamps.Count - 1] - timestamps[0]).TotalSeconds;
            double frequency = values.Count > 1
                ? (values.Count - 1) / Math.Max(totalSeconds, 1e-6)
                : 0;

            var samples = values.Select(v => (T)Convert.ChangeType(v, typeof(T))).ToArray();

            return new Signal<T>(
                samples.AsMemory(),
                frequency,
                startTime,
                metadata.Unit,
                metadata.Tags,
                source,
                metadata.Metadata,
                metadata.Quality);
        }

        /// <summary>Returns true if a signal with the given source identifier exists.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task<bool> ExistsAsync(string source, CancellationToken cancellationToken = default)
        {
            var filePath = GetFilePath(source);
            return Task.FromResult(File.Exists(filePath));
        }

        /// <summary>Deletes a signal and its companion metadata file from the store.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task DeleteAsync(string source, CancellationToken cancellationToken = default)
        {
            var filePath = GetFilePath(source);
            var metaPath = GetMetadataFilePath(source);
            if (File.Exists(filePath)) File.Delete(filePath);
            if (File.Exists(metaPath)) File.Delete(metaPath);
            return Task.CompletedTask;
        }

        private string GetFilePath(string source)
        {
            var safeName = SanitizeFileName(source);
            return Path.Combine(_directoryPath, $"{safeName}.parquet");
        }

        private string GetMetadataFilePath(string source)
        {
            var safeName = SanitizeFileName(source);
            return Path.Combine(_directoryPath, $"{safeName}.meta.json");
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
            return string.IsNullOrEmpty(sanitized) ? "_signal" : sanitized;
        }

        private async Task WriteMetadataFile<T>(string source, Signal<T> signal, CancellationToken ct)
        {
            var metaPath = GetMetadataFilePath(source);
            var meta = new SignalMetadata
            {
                UnitName = signal.Unit?.ToString(),
                UnitType = signal.Unit?.GetType().AssemblyQualifiedName,
                Quality = signal.Quality,
                Tags = signal.Tags.Count > 0 ? CopyTags(signal.Tags) : null,
                MetadataEntries = signal.Metadata.Count > 0 ? CopyMetadata(signal.Metadata) : null
            };
            var json = JsonSerializer.Serialize(meta);
#if NET10_0
            await File.WriteAllTextAsync(metaPath, json, ct).ConfigureAwait(false);
#else
            File.WriteAllText(metaPath, json);
#endif
        }

        private async Task<SignalMetadata> ReadMetadataFile(string source, CancellationToken ct)
        {
            var metaPath = GetMetadataFilePath(source);
            if (!File.Exists(metaPath))
                return null;
#if NET10_0
            var json = await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false);
#else
            var json = File.ReadAllText(metaPath);
#endif
            return JsonSerializer.Deserialize<SignalMetadata>(json);
        }

        private static Dictionary<string, object> CopyMetadata(Metadata metadata)
        {
            var dict = new Dictionary<string, object>();
            foreach (var kvp in metadata)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }

        private static Dictionary<string, string> CopyTags(IReadOnlyDictionary<string, string> tags)
        {
            var dict = new Dictionary<string, string>();
            foreach (var kvp in tags)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }

        private sealed class SignalMetadata
        {
            public string UnitName { get; set; }
            public string UnitType { get; set; }
            public Quality Quality { get; set; }
            public Dictionary<string, string> Tags { get; set; }
            public Dictionary<string, object> MetadataEntries { get; set; }

            [JsonIgnore]
            public Enum Unit => DeserializeUnit(UnitName, UnitType);

            [JsonIgnore]
            public Metadata Metadata
            {
                get
                {
                    var m = new Metadata();
                    if (MetadataEntries != null)
                    {
                        foreach (var kvp in MetadataEntries)
                            m = m.With(kvp.Key, kvp.Value);
                    }
                    return m;
                }
            }

            private static Enum DeserializeUnit(string unitName, string unitType)
            {
                if (unitName == null || unitType == null)
                    return null;
                var type = Type.GetType(unitType);
                if (type == null || !type.IsEnum)
                    return null;
                if (Enum.IsDefined(type, unitName))
                    return (Enum)Enum.Parse(type, unitName);
                return null;
            }
        }
    }
}
