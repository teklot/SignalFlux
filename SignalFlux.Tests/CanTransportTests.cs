using System;
using System.Threading;
using System.Threading.Tasks;
using SignalFlux.Protocols.Can;

namespace SignalFlux.Tests
{
    public class CanTransportTests
    {
        private static CancellationToken CT => TestContext.Current.CancellationToken;

        [Fact]
        public async Task InMemory_SendThenRead_ReturnsFrame()
        {
            await using var transport = new InMemoryCanTransport();
            await transport.OpenAsync(CT);
            var frame = new CanFrame(0x123, new byte[] { 1, 2, 3 }, Timestamp.Zero);

            await transport.SendAsync(frame, CT);
            var received = await transport.ReadAsync(CT);

            Assert.Equal(frame, received);
        }

        [Fact]
        public async Task InMemory_FrameReceivedEvent_FiresOnSend()
        {
            await using var transport = new InMemoryCanTransport();
            await transport.OpenAsync(CT);
            var frame = new CanFrame(0x123, new byte[] { 9 }, Timestamp.Zero);

            CanFrame? captured = null;
            transport.FrameReceived += (sender, e) => captured = e.Frame;

            await transport.SendAsync(frame, CT);
            Assert.Equal(frame, captured);
        }

        [Fact]
        public async Task InMemory_SendBeforeOpen_Throws()
        {
            await using var transport = new InMemoryCanTransport();
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(new CanFrame(0x123, new byte[] { 1 }), CT));
        }

        [Fact]
        public async Task InMemory_Disposed_ThrowsObjectDisposed()
        {
            var transport = new InMemoryCanTransport();
            await transport.OpenAsync(CT);
            await transport.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.SendAsync(new CanFrame(0x123, new byte[] { 1 }), CT));
        }

        [Fact]
        public async Task InMemory_Close_StopsReads()
        {
            await using var transport = new InMemoryCanTransport();
            await transport.OpenAsync(CT);
            await transport.CloseAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadAsync(CT));
        }

        [Fact]
        public async Task SocketCan_OnNonLinux_ThrowsPlatformNotSupported()
        {
            if (OperatingSystem.IsLinux())
                return;
            var transport = new SocketCanTransport("can0");
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() => transport.OpenAsync(CT));
        }

        [Fact]
        public async Task Pcan_ThrowsDriverMissing()
        {
            var transport = new PcanTransport(0x51);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.OpenAsync(CT));
            Assert.Contains("PCAN", ex.Message);
        }

        [Fact]
        public async Task Kvaser_ThrowsDriverMissing()
        {
            var transport = new KvaserTransport(0);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.OpenAsync(CT));
            Assert.Contains("CANlib", ex.Message);
        }
    }
}