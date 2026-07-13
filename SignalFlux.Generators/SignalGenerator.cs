using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SignalFlux;
using UnitsNet.Units;

namespace SignalFlux.Generators
{
    /// <summary>Abstract base class for generating deterministic or pseudo-random test signals.</summary>
    public abstract class SignalGenerator
    {
        /// <summary>A human-readable name for the generator.</summary>
        public string Name { get; }
        /// <summary>The sampling frequency in Hz at which the generator produces samples.</summary>
        public double Frequency { get; }
        /// <summary>The peak amplitude of the generated signal.</summary>
        public double Amplitude { get; }
        /// <summary>A DC offset applied to the generated signal.</summary>
        public double Offset { get; }
        /// <summary>The UTC start time assigned to generated signals.</summary>
        public Timestamp StartTime { get; }

        /// <summary>Creates a new signal generator with the specified characteristics.</summary>
        /// <param name="name">A human-readable name (defaults to the type name).</param>
        /// <param name="frequency">Sampling frequency in Hz (must be positive).</param>
        /// <param name="amplitude">Peak amplitude.</param>
        /// <param name="offset">DC offset.</param>
        /// <param name="startTime">UTC start time (defaults to now).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="frequency"/> is zero or negative.</exception>
        protected SignalGenerator(
            string name,
            double frequency,
            double amplitude = 1.0,
            double offset = 0.0,
            Timestamp? startTime = null)
        {
            Name = name ?? GetType().Name;
            Frequency = frequency > 0 ? frequency : throw new ArgumentOutOfRangeException(nameof(frequency));
            Amplitude = amplitude;
            Offset = offset;
            StartTime = startTime ?? Timestamp.UtcNow;
        }

        /// <summary>Generates a single sample value at the specified time offset (in seconds).</summary>
        /// <param name="time">The time offset in seconds from the start of the signal.</param>
        public abstract double Generate(double time);

        /// <summary>Generates a complete <see cref="Signal{T}"/> with the specified number of samples.</summary>
        /// <param name="sampleCount">The number of samples to generate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="sampleCount"/> is zero or negative.</exception>
        public Signal<double> GenerateSignal(int sampleCount)
        {
            if (sampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));

            var samples = new double[sampleCount];
            double dt = 1.0 / Frequency;

            for (int i = 0; i < sampleCount; i++)
            {
                double time = i * dt;
                samples[i] = Generate(time);
            }

            return new Signal<double>(
                samples,
                Frequency,
                StartTime,
                unit: ElectricPotentialUnit.Volt,
                tags: new Dictionary<string, string> { { "Generator", Name } },
                source: $"SignalFlux.Generators.{GetType().Name}");
        }

#if NET10_0
        /// <summary>Streams a sequence of signal chunks asynchronously, with optional delay between chunks.</summary>
        /// <param name="samplesPerChunk">Number of samples per chunk.</param>
        /// <param name="totalChunks">Total number of chunks to generate.</param>
        /// <param name="interval">Optional delay between chunks to simulate real-time acquisition.</param>
        public async IAsyncEnumerable<Signal<double>> GenerateStreaming(
            int samplesPerChunk,
            int totalChunks,
            TimeSpan? interval = null)
        {
            for (int chunk = 0; chunk < totalChunks; chunk++)
            {
                var timestamp = StartTime + TimeSpan.FromSeconds(
                    (double)(chunk * samplesPerChunk) / Frequency);

                yield return GenerateSignal(samplesPerChunk)
                    .WithStartTime(timestamp);

                if (interval.HasValue)
                    await Task.Delay(interval.Value);
            }
        }
#endif
    }
}
