using System.Linq;
using Xunit;
using SignalFlux.Protocols.Can;
using SignalFlux.Protocols.Can.Dbc;

namespace SignalFlux.Tests
{
    public class DbcParserTests
    {
        private const string SampleDbc = @"
VERSION ""1.0""

NS_ :
	NS_DESC_

BS_:

BU_: Engine ECU Vehicle_PCM

BO_ 256 EngineData: 8 Vehicle_PCM
 SG_ EngineRPM : 0|16@1+ (0.25,0) [0|16000] ""rpm""  Vehicle_PCM
 SG_ CoolantTemp : 32|8@1- (1,40) [0|215] ""degC""  Vehicle_PCM
 SG_ ThrottlePos : 7|16@0+ (0.1,0) [0|100] ""percent""  Vehicle_PCM

BO_ 512 VehicleSpeed: 8 Engine ECU
 SG_ VehicleSpeed : 0|16@1+ (1,0) [0|300] ""km/h""  Engine ECU

VAL_ 256 EngineRPM 0 ""Off"" 1 ""Idle"" 2 ""Cruise"" 3 ""Redline"";
";

        [Fact]
        public void Parse_ParsesMessages()
        {
            var db = DbcParser.Parse(SampleDbc);

            Assert.Equal(2, db.MessageCount);
            Assert.True(db.TryGetMessage(256, out DbcMessage engine));
            Assert.Equal("EngineData", engine.Name);
            Assert.Equal(8, engine.Length);
            Assert.Equal("Vehicle_PCM", engine.Transmitter);
            Assert.Equal(3, engine.Signals.Count);
        }

        [Fact]
        public void Parse_ParsesSignalLayout()
        {
            var db = DbcParser.Parse(SampleDbc);
            db.TryGetMessage(256, out DbcMessage engine);

            Assert.True(engine.TryGetSignal("EngineRPM", out DbcSignal rpm));
            Assert.Equal(0, rpm.StartBit);
            Assert.Equal(16, rpm.Length);
            Assert.Equal(CanByteOrder.BigEndian, rpm.ByteOrder);
            Assert.False(rpm.IsSigned);
            Assert.Equal(0.25, rpm.Factor);
            Assert.Equal(0.0, rpm.Offset);
            Assert.Equal("rpm", rpm.Unit);

            Assert.True(engine.TryGetSignal("CoolantTemp", out DbcSignal temp));
            Assert.Equal(CanByteOrder.BigEndian, temp.ByteOrder);
            Assert.True(temp.IsSigned);
            Assert.Equal(1.0, temp.Factor);
            Assert.Equal(40.0, temp.Offset);

            Assert.True(engine.TryGetSignal("ThrottlePos", out DbcSignal throttle));
            Assert.Equal(CanByteOrder.LittleEndian, throttle.ByteOrder);
        }

        [Fact]
        public void Parse_ParsesValueTable()
        {
            var db = DbcParser.Parse(SampleDbc);
            db.TryGetMessage(256, out DbcMessage engine);
            engine.TryGetSignal("EngineRPM", out DbcSignal rpm);

            Assert.Equal(4, rpm.ValueDescriptions.Count);
            Assert.Equal("Idle", rpm.ValueDescriptions[1]);
            Assert.Equal("Redline", rpm.ValueDescriptions[3]);
        }

        [Fact]
        public void Parse_EmptyString_GivesEmptyDatabase()
        {
            var db = DbcParser.Parse(string.Empty);
            Assert.Equal(0, db.MessageCount);
            Assert.Equal(0, db.SignalCount);
        }

        [Fact]
        public void SignalCount_TotalsAcrossMessages()
        {
            var db = DbcParser.Parse(SampleDbc);
            Assert.Equal(4, db.SignalCount);
        }
    }
}