using System.Collections.Generic;

namespace SignalFlux.Protocols.Can.Dbc
{
    /// <summary>A CAN message definition parsed from a DBC file, containing a set of signals.</summary>
    public sealed class DbcMessage
    {
        /// <summary>The message CAN identifier.</summary>
        public uint Id { get; set; }

        /// <summary>The message name.</summary>
        public string Name { get; set; }

        /// <summary>The message length in bytes (DLC).</summary>
        public int Length { get; set; }

        /// <summary>The transmitter name.</summary>
        public string Transmitter { get; set; }

        /// <summary>Comment text attached to the message (null if none).</summary>
        public string Comment { get; set; }

        /// <summary>The signals belonging to this message, keyed by name.</summary>
        public Dictionary<string, DbcSignal> Signals { get; } = new Dictionary<string, DbcSignal>();

        /// <summary>Returns true if the message contains a signal with the given name.</summary>
        public bool ContainsSignal(string name) => Signals.ContainsKey(name);

        /// <summary>Attempts to get the signal with the given name.</summary>
        public bool TryGetSignal(string name, out DbcSignal signal) => Signals.TryGetValue(name, out signal);

        /// <summary>Gets the multiplexor signal, or null if the message has no multiplexing.</summary>
        public DbcSignal Multiplexor
        {
            get
            {
                foreach (var signal in Signals.Values)
                {
                    if (signal.IsMultiplexor)
                        return signal;
                }
                return null;
            }
        }

        /// <summary>Returns a readable description of the message.</summary>
        public override string ToString() =>
            $"MSG 0x{Id:X3} {Name} ({Length} bytes, {Signals.Count} signals)";
    }
}
