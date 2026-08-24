namespace SignalFlux.Protocols.OpcUa
{
    /// <summary>Represents the state of an OPC UA connection.</summary>
    public enum OpcUaConnectionState
    {
        /// <summary>The adapter is disconnected from the server.</summary>
        Disconnected = 0,
        /// <summary>The adapter is establishing a connection.</summary>
        Connecting = 1,
        /// <summary>The adapter is connected and operational.</summary>
        Connected = 2,
        /// <summary>The connection was lost and reconnection is in progress.</summary>
        Reconnecting = 3,
    }

    /// <summary>Provides data for the <see cref="OpcUaConnectionAdapter.OnStateChanged"/> event.</summary>
    public sealed class OpcUaConnectionStateChangedEventArgs : System.EventArgs
    {
        /// <summary>The previous connection state.</summary>
        public OpcUaConnectionState PreviousState { get; }

        /// <summary>The new connection state.</summary>
        public OpcUaConnectionState NewState { get; }

        /// <summary>Creates event args describing a state transition.</summary>
        public OpcUaConnectionStateChangedEventArgs(OpcUaConnectionState previousState, OpcUaConnectionState newState)
        {
            PreviousState = previousState;
            NewState = newState;
        }
    }

    /// <summary>User authentication credentials for an OPC UA connection.</summary>
    public sealed class OpcUaUserCredentials
    {
        /// <summary>The user name.</summary>
        public string UserName { get; }

        /// <summary>The password.</summary>
        public string Password { get; }

        /// <summary>Creates a username/password credential set.</summary>
        public OpcUaUserCredentials(string userName, string password)
        {
            UserName = userName;
            Password = password;
        }
    }

    /// <summary>Options controlling how <see cref="OpcUaConnectionAdapter.ConnectAsync(string, OpcUaConnectionOptions, System.Threading.CancellationToken)"/> establishes a session.</summary>
    public sealed class OpcUaConnectionOptions
    {
        /// <summary>Application name reported to the server and used for the client certificate subject.</summary>
        public string ApplicationName { get; set; } = "SignalFlux";

        /// <summary>Whether to select an endpoint with message security (default false).</summary>
        public bool UseSecurity { get; set; } = false;

        /// <summary>Username/password credentials. Null (default) means anonymous access.</summary>
        public OpcUaUserCredentials UserCredentials { get; set; }

        /// <summary>Whether to automatically accept untrusted server certificates (dev convenience; disable for production).</summary>
        public bool AutoAcceptUntrustedCertificates { get; set; } = true;

        /// <summary>Whether to create the application instance certificate on first connect if missing.</summary>
        public bool CreateApplicationCertificate { get; set; } = true;

        /// <summary>Session timeout in milliseconds.</summary>
        public int SessionTimeoutMs { get; set; } = 60_000;

        /// <summary>Operation timeout in milliseconds for service calls.</summary>
        public int OperationTimeoutMs { get; set; } = 15_000;

        /// <summary>Delay between reconnect attempts in milliseconds.</summary>
        public int ReconnectPeriodMs { get; set; } = 5_000;

        /// <summary>Creates default options.</summary>
        public OpcUaConnectionOptions()
        {
        }
    }
}
