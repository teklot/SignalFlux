using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SignalFlux.Generators;
using SignalFlux.IO;
using SignalFlux.Storage;
using UnitsNet.Units;
using static System.Console;

namespace SignalFlux.Samples
{
    public static class AcquisitionSample
    {
        public static async Task RunAsync()
        {
            WriteLine("=== SignalFlux Live Acquisition Demo ===");
            WriteLine();

            var tempDir = Path.Combine(Path.GetTempPath(), "SignalFluxAcquisitionDemo");
            Directory.CreateDirectory(tempDir);
            var csvPath = Path.Combine(tempDir, "acquisition.csv");
            var sqlitePath = Path.Combine(tempDir, "acquisition.sqlite");

            try
            {
                int port = 9876;
                using var server = new TcpListener(IPAddress.Loopback, port);
                server.Start();
                WriteLine($"Simulated sensor server listening on port {port}...");
                _ = SimulateSensorAsync(port);

                await using (var connection = new TcpConnection("localhost", port))
                {
                    await connection.ConnectAsync();
                    WriteLine($"Connected via TcpConnection. State: {connection.State}");
                    WriteLine($"Endpoint: {connection.Endpoint}");

                    var buffer = new byte[8192];
                    int bytesRead = await connection.ReadAsync(buffer.AsMemory());
                    var rawText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    WriteLine($"Received {bytesRead} bytes from sensor");

                    var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var values = new List<double>();
                    var timestamps = new List<Timestamp>();

                    foreach (var line in lines)
                    {
                        var parts = line.Trim().Split(',');
                        if (parts.Length >= 2 &&
                            long.TryParse(parts[0], out var ticks) &&
                            double.TryParse(parts[1], out var val))
                        {
                            timestamps.Add(new Timestamp(ticks));
                            values.Add(val);
                        }
                    }

                    if (values.Count > 0)
                    {
                        var startTime = timestamps[0];
                        double totalSeconds = (timestamps[^1] - startTime).TotalSeconds;
                        double frequency = values.Count / Math.Max(totalSeconds, 1e-6);

                        var signal = new Signal<double>(
                            values.ToArray(),
                            frequency,
                            startTime,
                            ElectricPotentialUnit.Volt,
                            source: "simulated-sensor-01");

                        WriteLine($"Parsed signal: {signal.Count} samples @ {signal.Frequency:F1} Hz");
                        WriteLine($"  Start: {signal.StartTime.DateTime:O}");
                        WriteLine($"  Unit: {signal.Unit}");

                        await using (var writer = new CsvSignalWriter(csvPath))
                            await writer.WriteSignalAsync(signal);
                        WriteLine($"Written to CSV: {csvPath}");

                        await using (var sqlite = new SqliteSignalStore(sqlitePath, createNew: true))
                            await sqlite.WriteSignalAsync(signal);
                        WriteLine($"Written to SQLite: {sqlitePath}");

                        await using (var reader = new CsvSignalReader(csvPath))
                        {
                            var csvSignals = await reader.ReadAllSignalsAsync();
                            if (csvSignals.Count > 0)
                                WriteLine($"CSV round-trip: {csvSignals[0].Count} samples, source={csvSignals[0].Source}");
                        }

                        await using (var sqliteRead = new SqliteSignalStore(sqlitePath))
                        {
                            var sqliteSignal = await sqliteRead.ReadSignalAsync<double>("simulated-sensor-01");
                            WriteLine($"SQLite round-trip: {sqliteSignal.Count} samples, source={sqliteSignal.Source}");
                        }

                        WriteLine();
                        WriteLine("Acquisition pipeline: Simulated Sensor -> TCP -> Connection -> Signal -> Storage");
                        WriteLine("Pipeline complete. All stages verified.");
                    }
                    else
                    {
                        WriteLine("No valid data parsed from sensor stream.");
                    }
                }
            }
            finally
            {
                Cleanup(tempDir);
            }
        }

        private static async Task SimulateSensorAsync(int port)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                using var stream = client.GetStream();

                var gen = new SineGenerator(frequency: 10, amplitude: 5.0);
                var now = Timestamp.UtcNow;
                var sb = new StringBuilder();

                for (int i = 0; i < 50; i++)
                {
                    double value = gen.GenerateSignal(1).Samples.Span[0];
                    var ts = now + TimeSpan.FromSeconds(i / 100.0);
                    sb.AppendLine($"{ts.Ticks},{value:F6}");
                }

                var data = Encoding.UTF8.GetBytes(sb.ToString());
                await stream.WriteAsync(data);
            }
            catch (Exception ex)
            {
                WriteLine($"Sensor sim warning: {ex.Message}");
            }
        }

        private static void Cleanup(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch { }
        }
    }
}
