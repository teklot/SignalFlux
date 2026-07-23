using System;
using System.Collections.Generic;
using MavLinkSharp;
using MavLinkSharp.Enums;
using Xunit;
using SignalFlux.Protocols.Mavlink;

namespace SignalFlux.Tests
{
    public class MavlinkTests
    {
        public MavlinkTests()
        {
            MavLink.Initialize(DialectType.Common);
        }

        [Fact]
        public void ToMavlinkFrames_CreatesCorrectNumberOfFrames()
        {
            var signal = new Signal<double>(new double[] { 1.5, -0.5, 2.0 }, 10, Timestamp.Zero);
            var frames = signal.ToMavlinkFrames(messageId: 30, fieldName: "roll");

            Assert.Equal(3, frames.Count);
            Assert.All(frames, f => Assert.True(f.Length > 0));
        }

        [Fact]
        public void ToMavlinkFrames_ParsesBackToSignal()
        {
            var samples = new double[] { 1.5f, -0.5f, 2.0f };
            var signal = new Signal<double>(samples, 10, Timestamp.Zero);
            var frameBytes = signal.ToMavlinkFrames(messageId: 30, fieldName: "roll");

            var parsedFrames = new List<Frame>();
            foreach (var bytes in frameBytes)
            {
                var frame = new Frame();
                if (frame.TryParse(bytes))
                    parsedFrames.Add(frame);
            }

            Assert.Equal(3, parsedFrames.Count);

            var decoded = parsedFrames.ToSignal("roll", 10.0, Timestamp.Zero);
            var decodedSamples = decoded.Samples.ToArray();
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.Equal((float)samples[i], (float)decodedSamples[i], 4);
            }
        }

        [Fact]
        public void InitializeDialect_DoesNotThrow()
        {
            var exception = Record.Exception(() => MavlinkSignalExtensions.InitializeDialect(DialectType.Common));
            Assert.Null(exception);
        }

        [Fact]
        public void ToSignal_EmptyFrames_ReturnsEmptySignal()
        {
            var emptyFrames = new List<Frame>();
            var signal = emptyFrames.ToSignal("roll", 10.0, Timestamp.Zero);

            Assert.Equal(0, signal.Count);
        }

        [Fact]
        public void ToSignal_NullFrames_ReturnsEmptySignal()
        {
            List<Frame>? nullFrames = null;
            var signal = nullFrames.ToSignal("roll", 10.0, Timestamp.Zero);

            Assert.Equal(0, signal.Count);
        }
    }
}
