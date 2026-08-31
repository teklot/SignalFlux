using System;

namespace SignalFlux.Protocols.Can
{
    /// <summary>
    /// Represents a CAN data frame. This implementation focuses on standard (11-bit identifier) data frames,
    /// which carry a payload of up to 8 bytes. Extended frames and remote frames are represented but not
    /// treated as first-class encodings by the signal helpers.
    /// </summary>
    public readonly struct CanFrame : IEquatable<CanFrame>
    {
        /// <summary>The maximum payload length of a CAN data frame (8 bytes).</summary>
        public const int MaxPayloadLength = 8;

        /// <summary>The maximum standard (11-bit) identifier value.</summary>
        public const uint MaxStandardId = 0x7FF;

        /// <summary>The CAN identifier. For a standard frame this is the 11-bit arbitration ID.</summary>
        public uint Id { get; }

        /// <summary>The payload bytes (up to 8).</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>The number of payload bytes transported by the frame (the DLC tail is informational only).</summary>
        public int DataLength => Data.Length;

        /// <summary>Whether this is an extended (29-bit) identifier frame. Defaults to standard (11-bit).</summary>
        public bool IsExtended { get; }

        /// <summary>Whether this is a remote transmission request (RTR) frame with no payload.</summary>
        public bool IsRemoteRequest { get; }

        /// <summary>The timestamp at which the frame was sent or received (UTC).</summary>
        public Timestamp Timestamp { get; }

        /// <summary>Creates a standard-frame CAN frame.</summary>
        /// <param name="id">The 11-bit CAN identifier.</param>
        /// <param name="data">The payload bytes (up to 8).</param>
        /// <param name="timestamp">The frame timestamp; defaults to the current UTC time.</param>
        /// <param name="isRemoteRequest">Whether this is a remote transmission request.</param>
        public CanFrame(
            uint id,
            ReadOnlyMemory<byte> data,
            Timestamp? timestamp = null,
            bool isRemoteRequest = false)
        {
            if (data.Length > MaxPayloadLength)
                throw new ArgumentOutOfRangeException(nameof(data), "A CAN frame payload cannot exceed 8 bytes.");
            Id = id & MaxStandardId;
            Data = data;
            Timestamp = timestamp ?? Timestamp.Now;
            IsRemoteRequest = isRemoteRequest;
            IsExtended = false;
        }

        /// <summary>Gets the byte at the specified payload index.</summary>
        public byte this[int index] => Data.Span[index];

        /// <summary>Checks whether the frame is a valid standard data frame (id in range, payload within limits).</summary>
        public bool IsValid =>
            (Id & ~MaxStandardId) == 0 &&
            (IsRemoteRequest ? Data.Length == 0 : Data.Length <= MaxPayloadLength);

        /// <summary>Returns true if this frame is equal to another by id, payload, and flags.</summary>
        public bool Equals(CanFrame other) =>
            Id == other.Id &&
            Data.Span.SequenceEqual(other.Data.Span) &&
            IsExtended == other.IsExtended &&
            IsRemoteRequest == other.IsRemoteRequest;

        /// <summary>Returns true if this frame is equal to another object.</summary>
        public override bool Equals(object obj) => obj is CanFrame other && Equals(other);

        /// <summary>Returns a hash code for this frame.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)Id;
                hash = hash * 31 + Data.Length;
                hash = hash * 31 + (IsExtended ? 1 : 0);
                hash = hash * 31 + (IsRemoteRequest ? 1 : 0);
                return hash;
            }
        }

        /// <summary>Returns true if two frames are equal.</summary>
        public static bool operator ==(CanFrame left, CanFrame right) => left.Equals(right);
        /// <summary>Returns true if two frames are not equal.</summary>
        public static bool operator !=(CanFrame left, CanFrame right) => !left.Equals(right);

        /// <summary>Returns a string representation of this frame.</summary>
        public override string ToString() =>
            $"CAN [{Id:X3}] {DataLength:D1} {(Data.Length > 0 ? BitConverter.ToString(Data.ToArray()) : string.Empty)}".Trim();
    }
}
