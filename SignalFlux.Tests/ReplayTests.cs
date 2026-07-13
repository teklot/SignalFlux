using System;
using System.Collections.Generic;
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
    public class ReplayTests
    {
        private static CancellationToken CT => TestContext.Current.CancellationToken;
        [Fact]
        public void CanReplay_ChecksSessionFlag()
        {
            var session = new Session("S1", canReplay: true);
            var noReplay = new Session("S2", canReplay: false);
            var store = new InMemorySignalStore();
            var replayer = new SignalReplayer(store);

            Assert.True(replayer.CanReplay(session));
            Assert.False(replayer.CanReplay(noReplay));
        }

        [Fact]
        public async Task ReplaySignalAsync_ReturnsStoredSignal()
        {
            var store = new InMemorySignalStore();
            var now = Timestamp.UtcNow;
            var original = new Signal<double>(
                new double[] { 1.0, 2.0, 3.0 },
                100, now, ElectricPotentialUnit.Volt, source: "replay-test");

            await store.WriteSignalAsync(original, CT);
            var replayer = new SignalReplayer(store);
            var replayed = await replayer.ReplaySignalAsync<double>("replay-test", CT);

            Assert.Equal(original.Count, replayed.Count);
            Assert.Equal(original.Frequency, replayed.Frequency);
        }

        [Fact]
        public async Task ReplayStreamingAsync_ReturnsAllChunks()
        {
            var store = new InMemorySignalStore();
            var now = Timestamp.UtcNow;
            var original = new Signal<double>(
                new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 },
                100, now, ElectricPotentialUnit.Volt, source: "stream-replay");

            await store.WriteSignalAsync(original, CT);
            var replayer = new SignalReplayer(store);

            int chunkCount = 0;
            int totalSamples = 0;
            await foreach (var chunk in replayer.ReplayStreamingAsync<double>("stream-replay", 2, useOriginalTiming: false, cancellationToken: CT))
            {
                chunkCount++;
                totalSamples += chunk.Count;
            }

            Assert.Equal(3, chunkCount);
            Assert.Equal(original.Count, totalSamples);
        }

        [Fact]
        public async Task ReplaySessionSignalsAsync_WithoutReplayFlag_Throws()
        {
            var store = new InMemorySignalStore();
            var session = new Session("no-replay", canReplay: false);
            var replayer = new SignalReplayer(store);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in replayer.ReplaySessionSignalsAsync<double>(session, 10, useOriginalTiming: false, cancellationToken: CT))
                { }
            });
        }

        [Fact]
        public async Task ReplaySignalAsync_RoundTripsValues()
        {
            var store = new InMemorySignalStore();
            var now = Timestamp.UtcNow;
            var original = new Signal<double>(
                new double[] { 1.5, 2.5, 3.5, 4.5 },
                50, now, ElectricPotentialUnit.Volt, source: "value-test");

            await store.WriteSignalAsync(original, CT);
            var replayer = new SignalReplayer(store);
            var replayed = await replayer.ReplaySignalAsync<double>("value-test", CT);

            var origSpan = original.Samples.Span;
            var replaySpan = replayed.Samples.Span;
            for (int i = 0; i < original.Count; i++)
                Assert.Equal(origSpan[i], replaySpan[i]);
        }

        private sealed class InMemorySignalStore : ISignalStore
        {
            private readonly Dictionary<string, object> _signals = new();

            public Task WriteSignalAsync<T>(Signal<T> signal, CancellationToken cancellationToken = default)
            {
                _signals[signal.Source] = signal;
                return Task.CompletedTask;
            }

            public Task<Signal<T>> ReadSignalAsync<T>(string source, CancellationToken cancellationToken = default)
            {
                if (_signals.TryGetValue(source, out var obj) && obj is Signal<T> signal)
                    return Task.FromResult(signal);
                throw new KeyNotFoundException($"Signal with source '{source}' not found.");
            }

            public Task<bool> ExistsAsync(string source, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_signals.ContainsKey(source));
            }

            public Task DeleteAsync(string source, CancellationToken cancellationToken = default)
            {
                _signals.Remove(source);
                return Task.CompletedTask;
            }
        }
    }
}
