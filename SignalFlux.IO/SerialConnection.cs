using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace SignalFlux.IO
{
    /// <summary>Serial port connection implementing <see cref="IStreamConnection"/>.</summary>
    public sealed class SerialConnection : IStreamConnection
    {
        private SerialPort _port;
        private ConnectionState _state;

        /// <summary>The current state of the serial connection.</summary>
        public ConnectionState State => _state;
        /// <summary>The configuration options for this connection.</summary>
        public ConnectionOptions Options { get; }
        /// <summary>The endpoint URI (serial://PORT?baud=RATE).</summary>
        public Uri Endpoint { get; }
        /// <summary>The serial port name (e.g., COM1, /dev/ttyS0).</summary>
        public string PortName { get; }
        /// <summary>The baud rate for the serial connection.</summary>
        public int BaudRate { get; }
        /// <summary>The parity setting.</summary>
        public Parity Parity { get; }
        /// <summary>The number of data bits.</summary>
        public int DataBits { get; }
        /// <summary>The stop bits setting.</summary>
        public StopBits StopBits { get; }

        /// <summary>Creates a serial port connection.</summary>
        /// <param name="portName">The port name (e.g., COM1).</param>
        /// <param name="baudRate">The baud rate (default 115200).</param>
        /// <param name="parity">Parity setting (default None).</param>
        /// <param name="dataBits">Data bits (default 8).</param>
        /// <param name="stopBits">Stop bits (default One).</param>
        /// <param name="options">Optional connection configuration.</param>
        public SerialConnection(
            string portName,
            int baudRate = 115200,
            Parity parity = Parity.None,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            ConnectionOptions options = null)
        {
            PortName = portName;
            BaudRate = baudRate;
            Parity = parity;
            DataBits = dataBits;
            StopBits = stopBits;
            Options = options ?? new ConnectionOptions();
            Endpoint = new Uri($"serial://{portName}?baud={baudRate}");
        }

        /// <summary>Opens the serial port connection.</summary>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
                return Task.CompletedTask;

            _state = ConnectionState.Connecting;

            try
            {
                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    ReadTimeout = (int)Options.ReadTimeout.TotalMilliseconds,
                    WriteTimeout = (int)Options.WriteTimeout.TotalMilliseconds
                };
                _port.Open();
                _state = ConnectionState.Connected;
            }
            catch
            {
                _state = ConnectionState.Disconnected;
                _port?.Dispose();
                _port = null;
                throw;
            }

            return Task.CompletedTask;
        }

        /// <summary>Closes the serial port connection.</summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectionState.Disconnecting;
            try
            {
                if (_port?.IsOpen == true)
                    _port.Close();
            }
            finally
            {
                _port?.Dispose();
                _port = null;
                _state = ConnectionState.Disconnected;
            }
            return Task.CompletedTask;
        }

        /// <summary>Reads data from the serial port into the provided buffer.</summary>
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            var arr = new byte[buffer.Length];
            int count = _port.Read(arr, 0, arr.Length);
            arr.AsMemory(0, count).CopyTo(buffer);
            return Task.FromResult(count);
        }

        /// <summary>Writes data to the serial port.</summary>
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();
            var arr = data.ToArray();
            _port.Write(arr, 0, arr.Length);
            return Task.CompletedTask;
        }

        /// <summary>Returns the underlying <see cref="SerialPort.BaseStream"/>.</summary>
        public Stream GetStream()
        {
            ThrowIfNotConnected();
            return _port.BaseStream;
        }

#if NET10_0
        /// <summary>Disposes the serial connection asynchronously.</summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
#else
        /// <summary>Disposes the serial connection.</summary>
        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
#endif

        private void ThrowIfNotConnected()
        {
            if (_state != ConnectionState.Connected)
                throw new InvalidOperationException($"Connection is not connected. Current state: {_state}");
        }
    }
}
