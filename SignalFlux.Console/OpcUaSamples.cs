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
                adapter = await OpcUaConnectionAdapter.ConnectAsync(DefaultServerUrl);
                WriteLine($"Connected to: {DefaultServerUrl}");
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
                    var measurement = await adapter.ReadNodeAsync("ns=2;s=Temperature");
                    WriteLine($"Read node 'ns=2;s=Temperature':");
                    WriteLine($"  Value:     {measurement.Value}");
                    WriteLine($"  Timestamp: {measurement.Timestamp}");
                    WriteLine($"  Quality:   {measurement.Quality}");
                }
                catch (Exception ex)
                {
                    WriteLine($"Read demo skipped (no test node): {ex.Message}");
                }
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
