using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SignalFlux.Storage
{
    /// <summary>SQLite-backed signal store providing read/write access to persisted signals.</summary>
    public sealed class SqliteSignalStore : ISignalStore, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private bool _disposed;

        /// <summary>Creates a store backed by the given SQLite connection string.</summary>
        /// <param name="connectionString">ADO.NET connection string for the SQLite database.</param>
        public SqliteSignalStore(string connectionString)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            InitializeSchema();
        }

        /// <summary>Creates a store backed by the given SQLite file, optionally creating a new database.</summary>
        /// <param name="filePath">Path to the SQLite database file.</param>
        /// <param name="createNew">If true, deletes any existing file and starts fresh.</param>
        public SqliteSignalStore(string filePath, bool createNew = false)
        {
            if (createNew && File.Exists(filePath))
                File.Delete(filePath);
            _connection = new SqliteConnection($"Data Source={filePath}");
            _connection.Open();
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS signals (
                    source            TEXT PRIMARY KEY,
                    data_type         TEXT NOT NULL,
                    frequency         REAL NOT NULL,
                    start_time_ticks  INTEGER NOT NULL,
                    unit_name         TEXT,
                    unit_type         TEXT,
                    quality           INTEGER NOT NULL,
                    metadata_json     TEXT,
                    tags_json         TEXT,
                    samples           BLOB NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        /// <summary>Writes a signal to the SQLite store, inserting or replacing by source.</summary>
        /// <param name="signal">The signal to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WriteSignalAsync<T>(Signal<T> signal, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var samplesJson = JsonSerializer.Serialize(signal.Samples.ToArray());
            var metadataJson = signal.Metadata.Count > 0
                ? JsonSerializer.Serialize(ToDictionary(signal.Metadata))
                : null;
            var tagsJson = signal.Tags.Count > 0
                ? JsonSerializer.Serialize(ToDictionary(signal.Tags))
                : null;
            var unitName = signal.Unit?.ToString();
            var unitType = signal.Unit?.GetType().AssemblyQualifiedName;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO signals
                    (source, data_type, frequency, start_time_ticks, unit_name, unit_type, quality, metadata_json, tags_json, samples)
                VALUES
                    (@source, @data_type, @frequency, @start_time_ticks, @unit_name, @unit_type, @quality, @metadata_json, @tags_json, @samples)";

            cmd.Parameters.AddWithValue("@source", signal.Source);
            cmd.Parameters.AddWithValue("@data_type", typeof(T).FullName);
            cmd.Parameters.AddWithValue("@frequency", signal.Frequency);
            cmd.Parameters.AddWithValue("@start_time_ticks", signal.StartTime.Ticks);
            cmd.Parameters.AddWithValue("@unit_name", (object)unitName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@unit_type", (object)unitType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@quality", (int)signal.Quality);
            cmd.Parameters.AddWithValue("@metadata_json", (object)metadataJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tags_json", (object)tagsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@samples", Encoding.UTF8.GetBytes(samplesJson));

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads a signal from the store by its source identifier.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="KeyNotFoundException">Thrown when no signal with the given source exists.</exception>
        public async Task<Signal<T>> ReadSignalAsync<T>(string source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT data_type, frequency, start_time_ticks, unit_name, unit_type, quality, metadata_json, tags_json, samples FROM signals WHERE source = @source";
            cmd.Parameters.AddWithValue("@source", source);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new KeyNotFoundException($"Signal with source '{source}' not found.");

            var dataType = reader.GetString(0);
            if (dataType != typeof(T).FullName)
                throw new InvalidOperationException($"Type mismatch: stored type is '{dataType}', requested '{typeof(T).FullName}'");

            var frequency = reader.GetDouble(1);
            var startTimeTicks = reader.GetInt64(2);
            var startTime = new Timestamp(startTimeTicks);
            var unitName = reader.IsDBNull(3) ? null : reader.GetString(3);
            var unitType = reader.IsDBNull(4) ? null : reader.GetString(4);
            var quality = (Quality)reader.GetInt32(5);
            var metadataJson = reader.IsDBNull(6) ? null : reader.GetString(6);
            var tagsJson = reader.IsDBNull(7) ? null : reader.GetString(7);

            var blob = reader.GetFieldValue<byte[]>(8);
            var samplesJson = Encoding.UTF8.GetString(blob);

            var samples = JsonSerializer.Deserialize<T[]>(samplesJson);
            Enum unit = DeserializeUnit(unitName, unitType);
            var metadata = new Metadata();
            if (metadataJson != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                foreach (var kvp in dict)
                    metadata = metadata.With(kvp.Key, kvp.Value);
            }
            IReadOnlyDictionary<string, string> tags = tagsJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson)
                : new Dictionary<string, string>();

            return new Signal<T>(
                samples.AsMemory(),
                frequency,
                startTime,
                unit,
                tags,
                source,
                metadata,
                quality);
        }

        /// <summary>Returns true if a signal with the given source identifier exists.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<bool> ExistsAsync(string source, CancellationToken cancellationToken = default)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM signals WHERE source = @source";
            cmd.Parameters.AddWithValue("@source", source);
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result) > 0;
        }

        /// <summary>Deletes a signal from the store.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteAsync(string source, CancellationToken cancellationToken = default)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM signals WHERE source = @source";
            cmd.Parameters.AddWithValue("@source", source);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

        private static Dictionary<string, object> ToDictionary(Metadata metadata)
        {
            var dict = new Dictionary<string, object>();
            foreach (var kvp in metadata)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }

        private static Dictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> source)
        {
            var dict = new Dictionary<string, string>();
            foreach (var kvp in source)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }

        /// <summary>Asynchronously disposes the underlying SQLite connection.</summary>
        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _connection.Dispose();
            }
            return default;
        }

        /// <summary>Disposes the underlying SQLite connection.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _connection.Dispose();
            }
        }
    }
}
