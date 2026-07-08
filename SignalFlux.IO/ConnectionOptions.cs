using System;

namespace SignalFlux.IO
{
    /// <summary>Configuration options for stream connections, including timeouts and buffer sizes.</summary>
    public sealed class ConnectionOptions
    {
        /// <summary>Maximum time to wait for a connection to be established. Default is 5 seconds.</summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        /// <summary>Maximum time to wait for a read operation to complete. Default is 10 seconds.</summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(10);
        /// <summary>Maximum time to wait for a write operation to complete. Default is 10 seconds.</summary>
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(10);
        /// <summary>Size of the receive buffer in bytes. Default is 65536.</summary>
        public int ReceiveBufferSize { get; set; } = 65536;
        /// <summary>Size of the send buffer in bytes. Default is 65536.</summary>
        public int SendBufferSize { get; set; } = 65536;
        /// <summary>Disables Nagle's algorithm when true (default).</summary>
        public bool NoDelay { get; set; } = true;
        /// <summary>Number of automatic reconnection attempts on failure. Default is 0 (no reconnect).</summary>
        public int ReconnectAttempts { get; set; } = 0;
        /// <summary>Delay between reconnection attempts. Default is 1 second.</summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Creates a deep copy of the current options.</summary>
        public ConnectionOptions Clone() =>
            new ConnectionOptions
            {
                ConnectTimeout = ConnectTimeout,
                ReadTimeout = ReadTimeout,
                WriteTimeout = WriteTimeout,
                ReceiveBufferSize = ReceiveBufferSize,
                SendBufferSize = SendBufferSize,
                NoDelay = NoDelay,
                ReconnectAttempts = ReconnectAttempts,
                ReconnectDelay = ReconnectDelay
            };
    }
}
