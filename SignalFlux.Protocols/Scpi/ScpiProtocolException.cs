using System;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>Thrown when a SCPI conversation violates the protocol (malformed headers, unexpected bytes, or truncated responses).</summary>
    public sealed class ScpiProtocolException : InvalidOperationException
    {
        /// <summary>Creates a protocol exception with the given message.</summary>
        /// <param name="message">The error description.</param>
        public ScpiProtocolException(string message)
            : base(message)
        {
        }
    }
}