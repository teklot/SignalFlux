using System;

namespace SignalFlux.Testing
{
    /// <summary>Generates a sinusoidal signal: Offset + Amplitude * sin(2π·SignalFrequency·t + Phase).</summary>
    public class SineGenerator : SignalGenerator
    {
        /// <summary>The frequency of the sine wave in Hz (distinct from the sampling rate).</summary>
        public double SignalFrequency { get; }

        /// <summary>The initial phase offset in radians.</summary>
        public double Phase { get; }

        /// <summary>Creates a sine wave generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="signalFrequency">Frequency of the sine wave in Hz (defaults to 1/10 of sampling rate).</param>
        /// <param name="amplitude">Peak amplitude.</param>
        /// <param name="offset">DC offset.</param>
        /// <param name="phase">Initial phase in radians.</param>
        /// <param name="startTime">UTC start time.</param>
        public SineGenerator(
            string name = "Sine",
            double frequency = 1000,
            double signalFrequency = 0,
            double amplitude = 1.0,
            double offset = 0.0,
            double phase = 0.0,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
            SignalFrequency = signalFrequency > 0 ? signalFrequency : frequency / 10.0;
            Phase = phase;
        }

        /// <summary>Generates the sine value at the given time offset.</summary>
        public override double Generate(double time) =>
            Offset + Amplitude * Math.Sin(2.0 * Math.PI * SignalFrequency * time + Phase);
    }
}
