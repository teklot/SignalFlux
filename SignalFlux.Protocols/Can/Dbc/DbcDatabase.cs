using System.Collections.Generic;

namespace SignalFlux.Protocols.Can.Dbc
{
    /// <summary>A parsed DBC database containing message and signal definitions.</summary>
    public sealed class DbcDatabase
    {
        /// <summary>The messages defined in the database, keyed by CAN identifier.</summary>
        public Dictionary<uint, DbcMessage> Messages { get; } = new Dictionary<uint, DbcMessage>();

        /// <summary>Attempts to get the message with the given CAN identifier.</summary>
        public bool TryGetMessage(uint id, out DbcMessage message) => Messages.TryGetValue(id, out message);

        /// <summary>Gets a message by its CAN identifier, or null.</summary>
        public DbcMessage GetMessage(uint id) => Messages.TryGetValue(id, out DbcMessage m) ? m : null;

        /// <summary>The number of messages in the database.</summary>
        public int MessageCount => Messages.Count;

        /// <summary>The total number of signals across all messages.</summary>
        public int SignalCount
        {
            get
            {
                int count = 0;
                foreach (var message in Messages.Values)
                    count += message.Signals.Count;
                return count;
            }
        }
    }
}
