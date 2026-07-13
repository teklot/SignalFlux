using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Storage
{
    /// <summary>Replays signals from an <see cref="ISignalStore"/> with optional original-timing support.</summary>
    public sealed class SignalReplayer
    {
        private readonly ISignalStore _store;

        /// <summary>Creates a replayer backed by the specified signal store.</summary>
        /// <param name="store">The signal store to read from.</param>
        public SignalReplayer(ISignalStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Returns true if the session's <see cref="Session.CanReplay"/> flag is set.</summary>
        public bool CanReplay(Session session) => session.CanReplay;

        /// <summary>Replays a single signal from the store.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<Signal<T>> ReplaySignalAsync<T>(string source, CancellationToken cancellationToken = default)
        {
            return await _store.ReadSignalAsync<T>(source, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Streams a signal in chunks with optional original-timing delay between chunks.</summary>
        /// <param name="source">The source identifier.</param>
        /// <param name="samplesPerChunk">Number of samples per yielded chunk.</param>
        /// <param name="useOriginalTiming">If true, inserts Task.Delay matching the original sample rate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async IAsyncEnumerable<Signal<T>> ReplayStreamingAsync<T>(
            string source,
            int samplesPerChunk,
            bool useOriginalTiming = true,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var signal = await _store.ReadSignalAsync<T>(source, cancellationToken).ConfigureAwait(false);
            var samples = signal.Samples.ToArray();
            var sampleInterval = TimeSpan.FromSeconds(1.0 / signal.Frequency);
            int index = 0;

            while (index < samples.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int count = Math.Min(samplesPerChunk, samples.Length - index);
                var chunk = new T[count];
                Array.Copy(samples, index, chunk, 0, count);

                var chunkSignal = new Signal<T>(
                    chunk.AsMemory(),
                    signal.Frequency,
                    signal.StartTime + TimeSpan.FromSeconds(index / signal.Frequency),
                    signal.Unit,
                    signal.Tags,
                    signal.Source,
                    signal.Metadata,
                    signal.Quality);

                yield return chunkSignal;

                index += count;

                if (useOriginalTiming && index < samples.Length)
                    await Task.Delay(TimeSpan.FromTicks(sampleInterval.Ticks * count), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Streams all signals in an experiment as chunks.</summary>
        /// <param name="experiment">The experiment whose signals to replay.</param>
        /// <param name="samplesPerChunk">Number of samples per yielded chunk.</param>
        /// <param name="useOriginalTiming">If true, inserts Task.Delay matching the original sample rate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async IAsyncEnumerable<Signal<T>> ReplayExperimentSignalsAsync<T>(
            Experiment experiment,
            int samplesPerChunk,
            bool useOriginalTiming = true,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var kvp in experiment.Signals)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (kvp.Value is Signal<T> sig)
                {
                    var source = sig.Source;
                    if (!string.IsNullOrEmpty(source))
                    {
                        await foreach (var chunk in ReplayStreamingAsync<T>(source, samplesPerChunk, useOriginalTiming, cancellationToken))
                            yield return chunk;
                    }
                }

                var signalKey = kvp.Key;
                if (await _store.ExistsAsync(signalKey, cancellationToken).ConfigureAwait(false))
                {
                    await foreach (var chunk in ReplayStreamingAsync<T>(signalKey, samplesPerChunk, useOriginalTiming, cancellationToken))
                        yield return chunk;
                }
            }
        }

        /// <summary>Streams all signals across all experiments in a session as chunks.</summary>
        /// <param name="session">The session whose experiments to replay.</param>
        /// <param name="samplesPerChunk">Number of samples per yielded chunk.</param>
        /// <param name="useOriginalTiming">If true, inserts Task.Delay matching the original sample rate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Session.CanReplay"/> is false.</exception>
        public async IAsyncEnumerable<Signal<T>> ReplaySessionSignalsAsync<T>(
            Session session,
            int samplesPerChunk,
            bool useOriginalTiming = true,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!session.CanReplay)
                throw new InvalidOperationException($"Session '{session.Id}' does not support replay (CanReplay is false).");

            foreach (var experiment in session.Experiments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await foreach (var chunk in ReplayExperimentSignalsAsync<T>(experiment, samplesPerChunk, useOriginalTiming, cancellationToken))
                    yield return chunk;
            }
        }
    }
}
