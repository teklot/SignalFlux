using System.Text;
using SignalFlux.Protocols.Scpi;
using UnitsNet.Units;
using static System.Console;

namespace SignalFlux.Console
{
    /// <summary>
    /// SCPI protocol codec demo: drives a simulated oscilloscope instrument
    /// over an in-memory connection, showing *IDN? identification, IEEE-488.2
    /// definite and indefinite waveform blocks, and Signal&lt;T&gt; reconstruction.
    /// </summary>
    public static class ScpiSamples
    {
        public static async Task RunScpiSample()
        {
            WriteLine("\n=== SCPI Protocol Codec Demo ===\n");

            double frequency = 50.0;
            var samples = GenerateSine(100, frequency, 3.3, sampleRate: 1000.0);
            double xIncrement = 1.0 / 1000.0;

            var connection = new InMemoryScpiConnection(Responder(samples, xIncrement));
            var scpi = new ScpiClient(connection, "\n");

            WriteLine("Connected to simulated oscilloscope (memory://scpi).");
            WriteLine();

            string idn = await scpi.IdnAsync();
            WriteLine($"*IDN? -> {idn}");
            WriteLine();

            var waveform = await scpi.QueryBlockAsync(":WAVeform:DATA?");
            WriteLine($":WAVeform:DATA? -> definite block (#{waveform.Length} bytes, {waveform.Length / 8} doubles)");
            WriteLine($"  first 4 samples: {Samples(waveform.ToDoubles(), 4)}");
            WriteLine();

            string xincText = await scpi.QueryAsync(":WAVeform:XINC?");
            WriteLine($":WAVeform:XINC? -> {xincText}");
            WriteLine();

            var signal = waveform.ToSignal(
                frequency: 1.0 / xIncrement,
                startTime: Timestamp.UtcNow,
                unit: ElectricPotentialUnit.Volt,
                source: "scpi:scope-sim");
            WriteLine($"Reconstructed Signal<{nameof(Double)}>: {signal.Count} samples @ {signal.Frequency} Hz");
            WriteLine($"  unit={signal.Unit}  source={signal.Source}  duration={signal.Duration.TotalSeconds:F3}s");
            WriteLine($"  first 4 samples: {Samples(signal.Samples.ToArray(), 4)}");
            WriteLine();

            WriteLine("Now sending an indefinite-length block (#0) to show the parser variant:");
            var indefinite = new InMemoryScpiConnection(IndefiniteResponder());
            var indefiniteClient = new ScpiClient(indefinite, "\n");
            var indefiniteBlock = await indefiniteClient.QueryBlockAsync(":WAVeform:DATA?");
            WriteLine($"  #0 block parsed: {indefiniteBlock.Length} bytes, {indefiniteBlock.ToDoubles().Length} doubles");
        }

        private static Func<string, byte[]?> Responder(double[] samples, double xIncrement)
        {
            return command =>
            {
                WriteLine($"  [instr] << {command}");
                switch (command.Trim().ToUpperInvariant())
                {
                    case "*IDN?":
                        return Line("SignalFlux,SimOscilloscope,SN-001,1.0");
                    case ":WAVEFORM:XINC?":
                        return Line(xIncrement.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    case ":WAVEFORM:DATA?":
                        return DefiniteBlock(samples);
                    default:
                        return Line("ERROR:UNKNOWN");
                }
            };
        }

        private static Func<string, byte[]?> IndefiniteResponder()
        {
            return command =>
            {
                if (string.Equals(command.Trim(), ":WAVeform:DATA?", StringComparison.OrdinalIgnoreCase))
                    return IndefiniteBlock(new double[] { 0.0, 0.5, 1.0, 0.5 });
                return Line("ERROR:UNKNOWN");
            };
        }

        private static double[] GenerateSine(int count, double hz, double amplitude, double sampleRate)
        {
            var values = new double[count];
            for (int i = 0; i < count; i++)
                values[i] = amplitude * Math.Sin(2.0 * Math.PI * hz * i / sampleRate);
            return values;
        }

        private static byte[] DefiniteBlock(double[] values)
        {
            var payload = EncodeDoubles(values);
            string length = payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var header = Encoding.ASCII.GetBytes("#" + length.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + length);
            return Concat(header, payload);
        }

        private static byte[] IndefiniteBlock(double[] values)
        {
            var payload = EncodeDoubles(values);
            return Concat(Encoding.ASCII.GetBytes("#0"), payload, new byte[] { (byte)'\n' });
        }

        private static byte[] EncodeDoubles(double[] values)
        {
            var payload = new byte[values.Length * 8];
            for (int i = 0; i < values.Length; i++)
            {
                byte[] raw = BitConverter.GetBytes(values[i]);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(raw); // SCPI waveform encoding is big-endian
                Array.Copy(raw, 0, payload, i * 8, 8);
            }
            return payload;
        }

        private static byte[] Line(string text)
            => Concat(Encoding.ASCII.GetBytes(text), new byte[] { (byte)'\n' });

        private static byte[] Concat(params byte[][] parts)
        {
            int total = 0;
            foreach (var part in parts)
                total += part.Length;
            var result = new byte[total];
            int offset = 0;
            foreach (var part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        private static string Samples(double[] values, int count)
        {
            var parts = new List<string>();
            for (int i = 0; i < Math.Min(count, values.Length); i++)
                parts.Add(values[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(", ", parts);
        }
    }
}