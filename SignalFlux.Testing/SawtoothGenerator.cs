using System;

namespace SignalFlux.Testing
{
    /// <summary>Generates a sawtooth wave signal that rises linearly from -Amplitude to +Amplitude each period.</summary>
    public class SawtoothGenerator : SignalGenerator
    {
        /// <summary>Creates a sawtooth wave generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="amplitude">Peak amplitude.</param>
        /// <param name="offset">DC offset.</param>
        /// <param name="startTime">UTC start time.</param>
        public SawtoothGenerator(
            string name = "Sawtooth",
            double frequency = 1000,
            double amplitude = 1.0,
            double offset = 0.0,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
        }

        /// <summary>Generates the sawtooth value at the given time offset.</summary>
        public override double Generate(double time)
        {
            double period = 1.0 / Frequency;
            double t = time % period;
            return Offset + Amplitude * (2.0 * t / period - 1.0);
        }
    }
}
