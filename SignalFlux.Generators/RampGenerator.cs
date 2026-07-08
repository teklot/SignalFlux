using System;

namespace SignalFlux.Generators
{
    /// <summary>Generates a linear ramp signal: Offset + Slope × time.</summary>
    public class RampGenerator : SignalGenerator
    {
        /// <summary>The slope of the ramp in units per second.</summary>
        public double Slope { get; }

        /// <summary>Creates a ramp generator.</summary>
        /// <param name="name">Generator name.</param>
        /// <param name="frequency">Sampling frequency in Hz.</param>
        /// <param name="amplitude">Peak amplitude (used for metadata).</param>
        /// <param name="offset">DC offset / initial value.</param>
        /// <param name="slope">Slope in units per second.</param>
        /// <param name="startTime">UTC start time.</param>
        public RampGenerator(
            string name = "Ramp",
            double frequency = 1000,
            double amplitude = 1.0,
            double offset = 0.0,
            double slope = 1.0,
            Timestamp? startTime = null)
            : base(name, frequency, amplitude, offset, startTime)
        {
            Slope = slope;
        }

        /// <summary>Generates the ramp value at the given time offset.</summary>
        public override double Generate(double time) =>
            Offset + Slope * time;
    }
}
