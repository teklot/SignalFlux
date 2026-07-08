using System;

namespace SignalFlux.Generators
{
    /// <summary>Generates a square wave signal with configurable duty cycle.</summary>
    public class SquareGenerator : SignalGenerator
    {
        /// <summary>The duty cycle (fraction of the period spent at the high value), clamped to [0, 1].</summary>
        public double DutyCycle { get; }

        /// <summary>Creates a square wave generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="amplitude">Peak amplitude.</param>
        /// <param name="offset">DC offset.</param>
        /// <param name="dutyCycle">Duty cycle (0.0 to 1.0).</param>
        /// <param name="startTime">UTC start time.</param>
        public SquareGenerator(
            string name = "Square",
            double frequency = 1000,
            double amplitude = 1.0,
            double offset = 0.0,
            double dutyCycle = 0.5,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
            DutyCycle = dutyCycle < 0.0 ? 0.0 : (dutyCycle > 1.0 ? 1.0 : dutyCycle);
        }

        /// <summary>Generates the square wave value at the given time offset.</summary>
        public override double Generate(double time)
        {
            double period = 1.0 / Frequency;
            double t = time % period;
            return Offset + (t < period * DutyCycle ? Amplitude : -Amplitude);
        }
    }
}
