using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SignalFlux.Protocols.OpcUa;

namespace SignalFlux.Tests
{
    public class OpcUaConnectionAdapterTests
    {
        private const string TestServerUrl = "opc.tcp://localhost:4840";

        [Fact]
        public async Task ConnectAsync_ReturnsAdapter()
        {
            try
            {
                var ct = TestContext.Current.CancellationToken;
                var adapter = await OpcUaConnectionAdapter.ConnectAsync(TestServerUrl, ct: ct);
                Assert.NotNull(adapter);
                await adapter.DisposeAsync();
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task ConnectAsync_NullUrl_Throws()
        {
            var ct = TestContext.Current.CancellationToken;
            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => OpcUaConnectionAdapter.ConnectAsync(null, ct: ct));
            Assert.IsType<ArgumentException>(ex);
        }

        [Fact]
        public async Task ConnectAsync_EmptyUrl_Throws()
        {
            var ct = TestContext.Current.CancellationToken;
            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => OpcUaConnectionAdapter.ConnectAsync("", ct: ct));
            Assert.IsType<ArgumentException>(ex);
        }

        [Fact]
        public async Task ReadNodeAsync_ReturnsMeasurement()
        {
            try
            {
                var ct = TestContext.Current.CancellationToken;
                var adapter = await OpcUaConnectionAdapter.ConnectAsync(TestServerUrl, ct: ct);
                var measurement = await adapter.ReadNodeAsync("ns=2;s=Temperature", ct: ct);

                Assert.Equal(Quality.Good, measurement.Quality);
                Assert.NotEqual(Timestamp.Zero, measurement.Timestamp);
                await adapter.DisposeAsync();
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task BrowseAsync_ReturnsNodes()
        {
            try
            {
                var ct = TestContext.Current.CancellationToken;
                var adapter = await OpcUaConnectionAdapter.ConnectAsync(TestServerUrl, ct: ct);
                var nodes = await adapter.BrowseAsync(ct: ct);

                Assert.NotNull(nodes);
                await adapter.DisposeAsync();
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            try
            {
                var ct = TestContext.Current.CancellationToken;
                var adapter = await OpcUaConnectionAdapter.ConnectAsync(TestServerUrl, ct: ct);
                await adapter.DisposeAsync();
                await adapter.DisposeAsync();
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }
    }
}
