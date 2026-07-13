using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SignalFlux.Storage
{
    /// <summary>SQLite-backed experiment store providing read/write access to persisted experiments.</summary>
    public sealed class SqliteExperimentStore : IExperimentStore, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private bool _disposed;

        /// <summary>Creates a store backed by the given SQLite connection string.</summary>
        /// <param name="connectionString">ADO.NET connection string for the SQLite database.</param>
        public SqliteExperimentStore(string connectionString)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            InitializeSchema();
        }

        /// <summary>Creates a store backed by the given SQLite file, optionally creating a new database.</summary>
        /// <param name="filePath">Path to the SQLite database file.</param>
        /// <param name="createNew">If true, deletes any existing file and starts fresh.</param>
        public SqliteExperimentStore(string filePath, bool createNew = false)
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
                CREATE TABLE IF NOT EXISTS experiments (
                    id                  TEXT PRIMARY KEY,
                    operator            TEXT,
                    start_ticks         INTEGER NOT NULL,
                    end_ticks           INTEGER,
                    equipment_json      TEXT,
                    configuration_json  TEXT,
                    tags_json           TEXT,
                    signal_names_json   TEXT
                )";
            cmd.ExecuteNonQuery();
        }

        /// <summary>Writes an experiment to the store, inserting or replacing by id.</summary>
        /// <param name="experiment">The experiment to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task WriteExperimentAsync(Experiment experiment, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var equipmentJson = experiment.Equipment.Count > 0
                ? JsonSerializer.Serialize(experiment.Equipment)
                : null;
            var configJson = experiment.Configuration.Count > 0
                ? JsonSerializer.Serialize(ToDictionary(experiment.Configuration))
                : null;
            var tagsJson = experiment.Tags.Count > 0
                ? JsonSerializer.Serialize(ToDictionary(experiment.Tags))
                : null;
            var signalNamesJson = experiment.Signals.Count > 0
                ? JsonSerializer.Serialize(experiment.Signals.Keys.ToList())
                : null;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO experiments
                    (id, operator, start_ticks, end_ticks, equipment_json, configuration_json, tags_json, signal_names_json)
                VALUES
                    (@id, @operator, @start_ticks, @end_ticks, @equipment_json, @configuration_json, @tags_json, @signal_names_json)";

            cmd.Parameters.AddWithValue("@id", experiment.Id);
            cmd.Parameters.AddWithValue("@operator", experiment.Operator ?? "");
            cmd.Parameters.AddWithValue("@start_ticks", experiment.Start.Ticks);
            cmd.Parameters.AddWithValue("@end_ticks", experiment.End?.Ticks ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@equipment_json", (object)equipmentJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@configuration_json", (object)configJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tags_json", (object)tagsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@signal_names_json", (object)signalNamesJson ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads an experiment from the store by its identifier.</summary>
        /// <param name="id">The experiment identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="KeyNotFoundException">Thrown when no experiment with the given id exists.</exception>
        public async Task<Experiment> ReadExperimentAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT operator, start_ticks, end_ticks, equipment_json, configuration_json, tags_json, signal_names_json FROM experiments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new KeyNotFoundException($"Experiment with id '{id}' not found.");

            var operatorName = reader.GetString(0);
            var startTicks = reader.GetInt64(1);
            var start = new Timestamp(startTicks);
            Timestamp? end = reader.IsDBNull(2) ? null : new Timestamp(reader.GetInt64(2));
            var equipmentJson = reader.IsDBNull(3) ? null : reader.GetString(3);
            var configJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            var tagsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
            var signalNamesJson = reader.IsDBNull(6) ? null : reader.GetString(6);

            var equipment = equipmentJson != null
                ? JsonSerializer.Deserialize<List<string>>(equipmentJson)
                : new List<string>();
            var config = configJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(configJson)
                : new Dictionary<string, object>();
            var tags = tagsJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson)
                : new Dictionary<string, string>();

            return new Experiment(
                id: id,
                signals: new Dictionary<string, object>(),
                @operator: operatorName,
                configuration: config,
                start: start,
                end: end,
                equipment: equipment,
                tags: tags);
        }

        /// <summary>Returns true if an experiment with the given identifier exists.</summary>
        /// <param name="id">The experiment identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM experiments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result) > 0;
        }

        /// <summary>Deletes an experiment from the store.</summary>
        /// <param name="id">The experiment identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM experiments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static Dictionary<string, object> ToDictionary(IReadOnlyDictionary<string, object> source)
        {
            var dict = new Dictionary<string, object>();
            foreach (var kvp in source)
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
