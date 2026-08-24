using SignalFlux.Protocols.OpcUa;
using static System.Console;

namespace SignalFlux.Console
{
    public static class OpcUaSamples
    {
        private const string DefaultServerUrl = "opc.tcp://localhost:4840";

        public static async Task RunSampleAsync()
        {
            WriteLine("\n=== OPC UA Protocol Demo ===");
            WriteLine();

            OpcUaConnectionAdapter? adapter = null;
            try
            {
                var options = new OpcUaConnectionOptions
                {
                    ApplicationName = "SignalFlux.Demo",
                };

                adapter = await OpcUaConnectionAdapter.ConnectAsync(DefaultServerUrl, options);
                adapter.OnStateChanged += (sender, e) =>
                    WriteLine($"[state] {e.PreviousState} -> {e.NewState}");

                WriteLine($"Connected to: {DefaultServerUrl} (state: {adapter.State})");
                WriteLine();

                var nodes = await adapter.BrowseAsync();
                WriteLine($"Browsed {nodes.Count} nodes from root:");
                foreach (var node in nodes.Take(10))
                    WriteLine($"  {node}");
                if (nodes.Count > 10)
                    WriteLine($"  ... and {nodes.Count - 10} more");
                WriteLine();

                try
                {
                    var measurement = await adapter.ReadNodeWithUnitAsync("ns=2;s=Temperature");
                    WriteLine("Read node 'ns=2;s=Temperature':");
                    WriteLine($"  Value:     {measurement.Value}");
                    WriteLine($"  Unit:      {measurement.Unit?.GetType().Name ?? "(none)"}");
                    WriteLine($"  Timestamp: {measurement.Timestamp}");
                    WriteLine($"  Quality:   {measurement.Quality}");

                    try
                    {
                        await adapter.WriteNodeAsync("ns=2;s=Temperature", measurement.Value);
                        WriteLine("  Write back succeeded.");
                    }
                    catch (Exception writeEx)
                    {
                        WriteLine($"  Write skipped (node may be read-only): {writeEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    WriteLine($"Read demo skipped (no test node): {ex.Message}");
                }

                WriteLine();
            }
            catch (Exception ex)
            {
                WriteLine($"OPC UA demo skipped (no server at {DefaultServerUrl}): {ex.Message}");
            }
            finally
            {
                if (adapter != null)
                    await adapter.DisposeAsync();
            }
        }
    }
}
