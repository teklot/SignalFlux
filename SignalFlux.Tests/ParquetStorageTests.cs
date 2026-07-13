using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SignalFlux.Storage;
using UnitsNet.Units;
using Xunit;
using Xunit.v3;

namespace SignalFlux.Tests
{
    public class ParquetStorageTests
    {
        private static CancellationToken CT => TestContext.Current.CancellationToken;
        private static string GetTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public async Task WriteThenReadSignal_RoundTripsCorrectly()
        {
            var dir = GetTempDir();
            try
            {
                var now = Timestamp.UtcNow;
                var original = new Signal<double>(
                    new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 },
                    100, now, ElectricPotentialUnit.Volt, source: "parquet-test");

                var store = new ParquetSignalStore(dir);
                await store.WriteSignalAsync(original, CT);

                var read = await store.ReadSignalAsync<double>("parquet-test", CT);
                Assert.Equal(original.Count, read.Count);
                Assert.Equal(original.Source, read.Source);
                Assert.Equal("Volt", read.Unit.ToString());
                Assert.True(Math.Abs(original.Frequency - read.Frequency) < 1.0);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task ExistsAsync_ReturnsExpected()
        {
            var dir = GetTempDir();
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    new double[] { 1.0 }, 100, now, ElectricPotentialUnit.Volt, source: "exists-pq");

                var store = new ParquetSignalStore(dir);
                await store.WriteSignalAsync(signal, CT);

                Assert.True(await store.ExistsAsync("exists-pq", CT));
                Assert.False(await store.ExistsAsync("nonexistent", CT));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task DeleteAsync_RemovesSignal()
        {
            var dir = GetTempDir();
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    new double[] { 1.0 }, 100, now, ElectricPotentialUnit.Volt, source: "delete-pq");

                var store = new ParquetSignalStore(dir);
                await store.WriteSignalAsync(signal, CT);
                Assert.True(await store.ExistsAsync("delete-pq", CT));
                await store.DeleteAsync("delete-pq", CT);
                Assert.False(await store.ExistsAsync("delete-pq", CT));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task ReadNonexistentSignal_Throws()
        {
            var dir = GetTempDir();
            try
            {
                var store = new ParquetSignalStore(dir);
                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => store.ReadSignalAsync<double>("nonexistent", CT));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
