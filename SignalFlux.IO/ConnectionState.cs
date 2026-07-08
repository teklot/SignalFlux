namespace SignalFlux.IO
{
    /// <summary>Defines the lifecycle states of a stream connection.</summary>
    public enum ConnectionState
    {
        /// <summary>The connection has not been established or has been closed.</summary>
        Disconnected = 0,
        /// <summary>The connection is in the process of being established.</summary>
        Connecting = 1,
        /// <summary>The connection is active and ready for data transfer.</summary>
        Connected = 2,
        /// <summary>The connection is in the process of shutting down.</summary>
        Disconnecting = 3,
        /// <summary>The connection has encountered an unrecoverable error.</summary>
        Faulted = 4
    }
}
