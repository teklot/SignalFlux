using System;

namespace SignalFlux.Testing
{
    /// <summary>Generates uniform white noise in the range [Offset - Amplitude, Offset + Amplitude].</summary>
    public class NoiseGenerator : SignalGenerator
    {
        private readonly Random _random;

        /// <summary>Creates a noise generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="amplitude">Peak amplitude.</param>
        /// <param name="offset">DC offset.</param>
        /// <param name="seed">Optional seed for reproducible noise sequences.</param>
        /// <param name="startTime">UTC start time.</param>
        public NoiseGenerator(
            string name = "Noise",
            double frequency = 1000,
            double amplitude = 1.0,
            double offset = 0.0,
            int? seed = null,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Generates a uniform random value at the given time offset.</summary>
        public override double Generate(double time) =>
            Offset + Amplitude * (_random.NextDouble() * 2.0 - 1.0);
    }
}
