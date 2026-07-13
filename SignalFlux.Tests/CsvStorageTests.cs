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
    public class CsvStorageTests
    {
        private static CancellationToken CT => TestContext.Current.CancellationToken;
        [Fact]
        public async Task WriteThenReadSignal_RoundTripsCorrectly()
        {
            var path = Path.GetTempFileName() + ".csv";
            try
            {
                var now = Timestamp.UtcNow;
                var original = new Signal<double>(
                    new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 },
                    100, now, ElectricPotentialUnit.Volt, source: "test-src");

                await using (var writer = new CsvSignalWriter(path))
                    await writer.WriteSignalAsync(original, CT);

                await using (var reader = new CsvSignalReader(path))
                {
                    var signals = await reader.ReadAllSignalsAsync(CT);
                    Assert.Single(signals);
                    var read = signals[0];
                    Assert.Equal(original.Count, read.Count);
                    Assert.Equal(original.Frequency, read.Frequency, 3);
                    Assert.Equal(original.Source, read.Source);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task WriteThenReadStreaming_ReturnsCorrectChunks()
        {
            var path = Path.GetTempFileName() + ".csv";
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 },
                    100, now, ElectricPotentialUnit.Volt, source: "stream-test");

                await using (var writer = new CsvSignalWriter(path))
                    await writer.WriteSignalAsync(signal, CT);

                await using (var reader = new CsvSignalReader(path))
                {
                    int chunkCount = 0;
                    await foreach (var chunk in reader.ReadStreamingAsync(3, cancellationToken: CT))
                    {
                        chunkCount++;
                        Assert.True(chunk.Count <= 3);
                    }
                    Assert.Equal(3, chunkCount);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task WriteExperiment_MetadataWrittenAsComments()
        {
            var path = Path.GetTempFileName() + ".csv";
            try
            {
                var experiment = new Experiment("EXP-TEST", @operator: "TestBot",
                    start: Timestamp.UtcNow);

                await using (var writer = new CsvSignalWriter(path))
                    await writer.WriteExperimentAsync(experiment, CT);

                var content = await File.ReadAllTextAsync(path, CT);
                Assert.Contains("EXP-TEST", content);
                Assert.Contains("TestBot", content);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task EmptySignal_WritesAndReads()
        {
            var path = Path.GetTempFileName() + ".csv";
            try
            {
                var now = Timestamp.UtcNow;
                var signal = new Signal<double>(
                    Array.Empty<double>(), 100, now, ElectricPotentialUnit.Volt, source: "empty");

                await using (var writer = new CsvSignalWriter(path))
                    await writer.WriteSignalAsync(signal, CT);

                await using (var reader = new CsvSignalReader(path))
                {
                    var signals = await reader.ReadAllSignalsAsync(CT);
                    Assert.Empty(signals);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
