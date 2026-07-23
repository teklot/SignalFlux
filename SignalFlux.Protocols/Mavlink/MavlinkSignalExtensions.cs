using System;
using System.Collections.Generic;
using MavLinkSharp;
using MavLinkSharp.Enums;

namespace SignalFlux.Protocols.Mavlink
{
    /// <summary>Extension methods for converting <see cref="Signal{T}"/> to and from MAVLink frames.</summary>
    public static class MavlinkSignalExtensions
    {
        /// <summary>Encodes a <see cref="Signal{T}"/> into MAVLink v2 frames for the specified message and field.</summary>
        /// <param name="signal">The signal to encode.</param>
        /// <param name="messageId">The MAVLink message ID (e.g., 30 for ATTITUDE).</param>
        /// <param name="fieldName">The field name within the message (e.g., "roll", "lat").</param>
        /// <param name="systemId">MAVLink system ID (default 1).</param>
        /// <param name="componentId">MAVLink component ID (default 1).</param>
        /// <returns>A list of byte arrays, each representing a complete MAVLink v2 packet.</returns>
        public static IReadOnlyList<byte[]> ToMavlinkFrames(
            this Signal<double> signal,
            uint messageId,
            string fieldName,
            byte systemId = 1,
            byte componentId = 1)
        {
            var samples = signal.Samples.ToArray();
            var frames = new List<byte[]>(samples.Length);
            var message = MavLinkSharp.Metadata.Messages[messageId];

            for (int i = 0; i < samples.Length; i++)
            {
                var frame = new Frame
                {
                    StartMarker = Protocol.V2.StartMarker,
                    SystemId = systemId,
                    ComponentId = componentId,
                    MessageId = messageId,
                    Message = message,
                    PacketSequence = (byte)(i % 256)
                };
                frame.SetFields(new Dictionary<string, object>
                {
                    { fieldName, (float)samples[i] }
                });
                frames.Add(frame.ToBytes());
            }
            return frames;
        }

        /// <summary>Decodes a list of parsed MAVLink frames into a <see cref="Signal{T}"/> by extracting the specified field.</summary>
        /// <param name="frames">The parsed MAVLink frames to decode.</param>
        /// <param name="fieldName">The field name to extract from each frame.</param>
        /// <param name="frequency">Signal frequency in Hz for the reconstructed signal.</param>
        /// <param name="startTime">Start timestamp of the signal.</param>
        /// <param name="source">Source identifier (default "mavlink").</param>
        /// <returns>A <see cref="Signal{T}"/> reconstructed from the frame field values.</returns>
        public static Signal<double> ToSignal(
            this IReadOnlyList<Frame> frames,
            string fieldName,
            double frequency,
            Timestamp startTime,
            string source = "mavlink")
        {
            if (frames == null || frames.Count == 0)
                return new Signal<double>(Array.Empty<double>(), frequency, startTime, source: source);

            var samples = new double[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                samples[i] = frames[i].GetSingle(fieldName);
            }
            return new Signal<double>(samples, frequency, startTime, source: source);
        }

        /// <summary>Initializes MavLinkSharp with the specified dialect. Must be called before any frame parsing.</summary>
        /// <param name="dialect">The dialect type to load (default Common).</param>
        public static void InitializeDialect(DialectType dialect = DialectType.Common)
        {
            MavLink.Initialize(dialect);
        }
    }
}
