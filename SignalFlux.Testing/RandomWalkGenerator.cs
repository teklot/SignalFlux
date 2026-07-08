using System;
using UnitsNet.Units;

namespace SignalFlux.Testing
{
    /// <summary>Generates a random walk (Brownian motion) signal. Does not support time-based generation.</summary>
    public class RandomWalkGenerator : SignalGenerator
    {
        private readonly Random _random;
        /// <summary>The maximum step size per sample.</summary>
        public double StepSize { get; }

        /// <summary>Creates a random walk generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="amplitude">Maximum absolute value (signal is clamped to ±Amplitude).</param>
        /// <param name="offset">Initial value.</param>
        /// <param name="stepSize">Maximum step size per sample.</param>
        /// <param name="seed">Optional seed for reproducible sequences.</param>
        /// <param name="startTime">UTC start time.</param>
        public RandomWalkGenerator(
            string name = "RandomWalk",
            double frequency = 1000,
            double amplitude = 1.0,
            double offset = 0.0,
            double stepSize = 0.1,
            int? seed = null,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
            StepSize = stepSize;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Not supported for random walk. Use <see cref="GenerateSignal"/> instead.</summary>
        /// <exception cref="InvalidOperationException">Always thrown.</exception>
        public override double Generate(double time) =>
            throw new InvalidOperationException(
                "RandomWalkGenerator does not support time-based generation. Use GenerateSignal() instead.");

        /// <summary>Generates a random walk signal with the specified number of samples.</summary>
        /// <param name="sampleCount">The number of samples to generate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="sampleCount"/> is zero or negative.</exception>
        public new Signal<double> GenerateSignal(int sampleCount)
        {
            if (sampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));

            var samples = new double[sampleCount];
            samples[0] = Offset;

            for (int i = 1; i < sampleCount; i++)
            {
                samples[i] = samples[i - 1] + (_random.NextDouble() * 2.0 - 1.0) * StepSize;
                double v = samples[i];
                samples[i] = v < -Amplitude ? -Amplitude : (v > Amplitude ? Amplitude : v);
            }

            return new Signal<double>(
                samples,
                Frequency,
                StartTime,
                unit: ElectricPotentialUnit.Volt,
                tags: new System.Collections.Generic.Dictionary<string, string> { { "Generator", Name } },
                source: $"SignalFlux.Testing.{GetType().Name}");
        }
    }
}
