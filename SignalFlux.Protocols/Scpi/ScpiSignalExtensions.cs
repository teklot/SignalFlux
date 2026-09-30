using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>Extends SCPI types to convert oscilloscope waveform responses into <see cref="Signal{T}"/> instances.</summary>
    public static class ScpiSignalExtensions
    {
        /// <summary>Converts an IEEE-488.2 binary block into a uniformly-sampled double signal.</summary>
        /// <param name="block">The block payload, decoded as big-endian IEEE-754 double-precision samples.</param>
        /// <param name="frequency">Sampling frequency in Hz (the reciprocal of the instrument x-increment).</param>
        /// <param name="startTime">UTC time of the first sample.</param>
        /// <param name="gain">Optional gain applied to each sample (default 1).</param>
        /// <param name="offset">Optional offset added to each sample (default 0).</param>
        /// <param name="unit">Optional unit of measurement (e.g., <c>ElectricPotentialUnit.Volt</c>).</param>
        /// <param name="source">Optional source identifier for the signal.</param>
        /// <returns>A signal derived from the block samples.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
        public static Signal<double> ToSignal(
            this ScpiBlock block,
            double frequency,
            Timestamp startTime,
            double gain = 1.0,
            double offset = 0.0,
            Enum? unit = null,
            string? source = "scpi")
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            double[] raw = block.ToDoubles();
            var samples = new double[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                samples[i] = raw[i] * gain + offset;
            return new Signal<double>(samples, frequency, startTime, unit: unit, source: source);
        }

        /// <summary>Queries an oscilloscope via SCPI and builds a <see cref="Signal{T}"/> from the returned waveform.</summary>
        /// <param name="client">The SCPI client connected to the oscilloscope.</param>
        /// <param name="channel">The waveform channel to query (default "CH1"); used only for tagging the source.</param>
        /// <param name="unit">Optional unit of measurement (defaults to <c>ElectricPotentialUnit.Volt</c>).</param>
        /// <param name="source">Optional source identifier (defaults to "scpi:&lt;channel&gt;").</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A signal whose frequency is derived from the instrument x-increment query.</returns>
        /// <exception cref="FormatException">Thrown when the x-increment response cannot be parsed.</exception>
        public static async Task<Signal<double>> QueryWaveformAsync(
            this ScpiClient client,
            string channel = "CH1",
            Enum? unit = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            ScpiBlock block = await client.QueryBlockAsync(":WAVeform:DATA?", cancellationToken).ConfigureAwait(false);
            string xincText = await client.QueryAsync(":WAVeform:XINC?", cancellationToken).ConfigureAwait(false);
            if (!double.TryParse(xincText, NumberStyles.Float, CultureInfo.InvariantCulture, out double xIncrement) || xIncrement <= 0)
                throw new FormatException($"Invalid :WAVeform:XINC? response '{xincText}'.");
            unit ??= UnitsNet.Units.ElectricPotentialUnit.Volt;
            return block.ToSignal(
                1.0 / xIncrement,
                Timestamp.UtcNow,
                unit: unit,
                source: source ?? "scpi:" + channel);
        }
    }
}