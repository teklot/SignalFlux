using MavLinkSharp;
using MavLinkSharp.Enums;
using SignalFlux.Protocols.Mavlink;
using SignalFlux.Protocols.Modbus;
using SignalFlux.Protocols.Nmea;
using static System.Console;

namespace SignalFlux.Console
{
    public static class ProtocolSamples
    {
        public static void RunModbusSample()
        {
            WriteLine("=== Modbus Protocol Demo ===");
            WriteLine();

            var original = new Signal<double>(
                new double[] { 23.5, 24.1, 23.8, 24.5, 25.0 },
                frequency: 1.0,
                startTime: Timestamp.UtcNow,
                source: "temperature-sensor");

            WriteLine($"Original signal: {original.Count} samples");
            foreach (var s in original.Samples.Span)
                Write($"  {s:F1}");
            WriteLine();

            var registers = original.ToModbusRegisters(scale: 10.0);
            WriteLine($"\nEncoded to Modbus registers (scale=10):");
            foreach (var r in registers)
                Write($"  {r}");
            WriteLine();

            var decoded = registers.ToSignal(frequency: 1.0, startTime: original.StartTime, scale: 10.0);
            WriteLine($"\nDecoded back to signal:");
            var decodedSamples = decoded.Samples.ToArray();
            for (int i = 0; i < decodedSamples.Length; i++)
                Write($"  {decodedSamples[i]:F1}");
            WriteLine();

        }

        public static void RunMavlinkSample()
        {
            WriteLine("\n=== MAVLink Protocol Demo ===");
            WriteLine();

            MavlinkSignalExtensions.InitializeDialect(DialectType.Common);
            WriteLine("Initialized MAVLink with Common dialect.");

            var attitude = new Signal<double>(
                new double[] { 0.15, -0.08, 0.22, 0.31, -0.05 },
                frequency: 10.0,
                startTime: Timestamp.UtcNow,
                source: "imu-roll");

            WriteLine($"Original signal (roll): {attitude.Count} samples");
            foreach (var s in attitude.Samples.Span)
                Write($"  {s:F3}");
            WriteLine();

            var frames = attitude.ToMavlinkFrames(messageId: 30, fieldName: "roll");
            WriteLine($"\nEncoded to {frames.Count} MAVLink ATTITUDE frames (message ID 30).");

            var parsedFrames = new List<Frame>();
            foreach (var bytes in frames)
            {
                var frame = new Frame();
                if (frame.TryParse(bytes))
                    parsedFrames.Add(frame);
            }
            WriteLine($"Parsed {parsedFrames.Count} frames successfully.");

            var decoded = parsedFrames.ToSignal("roll", attitude.Frequency, attitude.StartTime);
            WriteLine($"\nDecoded back to signal:");
            var decodedSamples = decoded.Samples.ToArray();
            for (int i = 0; i < decodedSamples.Length; i++)
                Write($"  {(float)decodedSamples[i]:F3}");
            WriteLine();

        }

        public static void RunNmeaSample()
        {
            WriteLine("\n=== NMEA 0183 Protocol Demo ===");
            WriteLine();

            var sentences = new[]
            {
                ("RMC", "$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,5.2,45.0,070426,,,A*19"),
                ("GGA", "$GPGGA,201530.00,4739.77420,N,00854.55940,E,1,08,1.0,488.0,M,47.0,M,,*42"),
                ("VTG", "$GPVTG,45.0,T,43.5,M,5.2,N,9.6,K,A*16"),
                ("GSA", "$GPGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.5,0.9,1.2*12"),
            };

            foreach (var (type, raw) in sentences)
            {
                var msg = NmeaParser.Messages.NmeaMessage.Parse(raw, null, true);
                if (msg == null) continue;

                WriteLine($"[{type}] {raw}");

                switch (msg)
                {
                    case NmeaParser.Messages.Rmc rmc:
                        WriteLine($"  latitude:  {rmc.ToMeasurement("latitude").Value:F6}");
                        WriteLine($"  longitude: {rmc.ToMeasurement("longitude").Value:F6}");
                        WriteLine($"  speed:     {rmc.ToMeasurement("speed").Value:F1} knots");
                        WriteLine($"  course:    {rmc.ToMeasurement("course").Value:F1} deg");
                        break;
                    case NmeaParser.Messages.Gga gga:
                        WriteLine($"  latitude:  {gga.ToMeasurement("latitude").Value:F6}");
                        WriteLine($"  altitude:  {gga.ToMeasurement("altitude").Value:F1} m");
                        WriteLine($"  hdop:      {gga.ToMeasurement("hdop").Value:F1}");
                        WriteLine($"  satellites: {gga.ToMeasurement("satellites").Value:F0}");
                        break;
                    case NmeaParser.Messages.Vtg vtg:
                        WriteLine($"  course:    {vtg.ToMeasurement("course").Value:F1} deg true");
                        WriteLine($"  speed:     {vtg.ToMeasurement("speed-knots").Value:F1} kn");
                        WriteLine($"  speed:     {vtg.ToMeasurement("speed-kph").Value:F1} km/h");
                        break;
                    case NmeaParser.Messages.Gsa gsa:
                        WriteLine($"  pdop:      {gsa.ToMeasurement("pdop").Value:F1}");
                        WriteLine($"  hdop:      {gsa.ToMeasurement("hdop").Value:F1}");
                        WriteLine($"  vdop:      {gsa.ToMeasurement("vdop").Value:F1}");
                        break;
                }
                WriteLine();
            }

        }
    }
}
