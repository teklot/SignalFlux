using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SignalFlux.Storage;
using UnitsNet.Units;
using Xunit;
using Xunit.v3;

namespace SignalFlux.Tests
{
    public class SqliteStorageTests
    {
        private static CancellationToken CT => TestContext.Current.CancellationToken;
        private static string GetTempDbPath() => Path.GetTempFileName() + ".sqlite";
        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        [Fact]
        public async Task WriteThenReadSignal_RoundTripsCorrectly()
        {
            var path = GetTempDbPath();
            try
            {
                var now = Timestamp.UtcNow;
                var original = new Signal<double>(
                    new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 },
                    100, now, ElectricPotentialUnit.Volt, source: "sqlite-test-src");

                await using (var store = new SqliteSignalStore(path, createNew: true))
                    await store.WriteSignalAsync(original, CT);

                await using (var store = new SqliteSignalStore(path, createNew: false))
                {
                    var read = await store.ReadSignalAsync<double>("sqlite-test-src", CT);
                    Assert.Equal(original.Count, read.Count);
                    Assert.Equal(original.Frequency, read.Frequency);
                    Assert.Equal(original.Source, read.Source);
                    Assert.Equal(original.Unit, read.Unit);
                }
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public async Task ExistsAsync_ReturnsExpected()
        {
            var path = GetTempDbPath();
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    new double[] { 1.0 }, 100, now, ElectricPotentialUnit.Volt, source: "exists-test");

                await using (var store = new SqliteSignalStore(path, createNew: true))
                    await store.WriteSignalAsync(signal, CT);

                await using (var store = new SqliteSignalStore(path, createNew: false))
                {
                    Assert.True(await store.ExistsAsync("exists-test", CT));
                    Assert.False(await store.ExistsAsync("nonexistent", CT));
                }
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public async Task DeleteAsync_RemovesSignal()
        {
            var path = GetTempDbPath();
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    new double[] { 1.0 }, 100, now, ElectricPotentialUnit.Volt, source: "delete-test");

                await using (var store = new SqliteSignalStore(path, createNew: true))
                {
                    await store.WriteSignalAsync(signal, CT);
                    Assert.True(await store.ExistsAsync("delete-test", CT));
                    await store.DeleteAsync("delete-test", CT);
                    Assert.False(await store.ExistsAsync("delete-test", CT));
                }
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public async Task ReadNonexistentSignal_Throws()
        {
            var path = GetTempDbPath();
            try
            {
                await using (var store = new SqliteSignalStore(path, createNew: true))
                {
                    await Assert.ThrowsAsync<KeyNotFoundException>(
                        () => store.ReadSignalAsync<double>("nonexistent", CT));
                }
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public async Task Experiment_RoundTripsCorrectly()
        {
            var path = GetTempDbPath();
            try
            {
                var now = Timestamp.UtcNow;
                var original = new Experiment(
                    id: "EXP-SQLITE-001",
                    @operator: "TestBot",
                    start: now,
                    equipment: new[] { "DAQ-01", "Sensor-01" },
                    tags: new Dictionary<string, string> { { "env", "test" } });

                await using (var store = new SqliteExperimentStore(path, createNew: true))
                    await store.WriteExperimentAsync(original, CT);

                await using (var store = new SqliteExperimentStore(path, createNew: false))
                {
                    var read = await store.ReadExperimentAsync("EXP-SQLITE-001", CT);
                    Assert.Equal(original.Id, read.Id);
                    Assert.Equal(original.Operator, read.Operator);
                }
            }
            finally
            {
                SafeDelete(path);
            }
        }
    }
}
