using System;
using System.Globalization;

namespace SignalFlux.Protocols.Modbus
{
    /// <summary>Extension methods for converting Signal&lt;T&gt; to and from Modbus register arrays with scale, offset, and clamping.</summary>
    public static class ModbusSignalExtensions
    {
        /// <summary>Encodes a Signal&lt;double&gt; into Modbus holding registers using the specified scale, offset, and clamping range.</summary>
        /// <param name="signal">The signal to encode.</param>
        /// <param name="scale">Multiplier applied to the sample value before conversion (e.g., 100 for two decimal places).</param>
        /// <param name="offset">Value subtracted from the scaled result before clamping (e.g., 0).</param>
        /// <param name="minValue">Minimum register value after scaling (default 0).</param>
        /// <param name="maxValue">Maximum register value after scaling (default ushort.MaxValue).</param>
        /// <returns>An array of ushort registers representing the encoded signal samples.</returns>
        public static ushort[] ToModbusRegisters(
            this Signal<double> signal,
            double scale = 1.0,
            double offset = 0.0,
            ushort minValue = 0,
            ushort maxValue = ushort.MaxValue)
        {
            var samples = signal.Samples.ToArray();
            var registers = new ushort[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                double scaled = samples[i] * scale - offset;
                double clamped = Math.Max(minValue, Math.Min(maxValue, scaled));
                registers[i] = (ushort)Math.Round(clamped, 0, MidpointRounding.AwayFromZero);
            }
            return registers;
        }

        /// <summary>Decodes an array of Modbus registers back into a Signal&lt;double&gt; using the specified scale and offset.</summary>
        /// <param name="registers">The register array to decode.</param>
        /// <param name="frequency">Sample frequency in Hz for the reconstructed signal.</param>
        /// <param name="startTime">Start timestamp of the signal.</param>
        /// <param name="scale">Multiplier to reverse the original scaling (e.g., 0.01 if original scale was 100).</param>
        /// <param name="offset">Value added back after reverse scaling.</param>
        /// <param name="source">Source identifier for the reconstructed signal.</param>
        /// <returns>A Signal&lt;double&gt; reconstructed from the registers.</returns>
        public static Signal<double> ToSignal(
            this ushort[] registers,
            double frequency,
            Timestamp startTime,
            double scale = 1.0,
            double offset = 0.0,
            string source = "modbus")
        {
            var samples = new double[registers.Length];
            for (int i = 0; i < registers.Length; i++)
            {
                samples[i] = (registers[i] + offset) / scale;
            }
            return new Signal<double>(samples, frequency, startTime, source: source);
        }
    }
}
