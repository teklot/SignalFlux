using SignalFlux;
using SignalFlux.Space.Ccsds;
using SignalFlux.Space.Pus;
using UnitsNet.Units;

namespace SignalFlux.Tests
{
    public class PusHousekeepingTests
    {
        private static readonly Timestamp TimeEpoch = Timestamp.FromUnixSeconds(0);

        private static readonly PusParameterDefinition[] Definitions =
        {
            new(0x01, "BatteryVoltage", PusParameterType.UInt16, ElectricPotentialUnit.Volt, scale: 0.01),
            new(0x02, "StarTrackerTemp", PusParameterType.Int16, TemperatureUnit.DegreeCelsius, scale: 0.1, minimum: 0, maximum: 100),
            new(0x03, "BusCurrent", PusParameterType.Float64, ElectricCurrentUnit.Ampere),
            new(0x04, "PowerOnFlag", PusParameterType.UInt8),
        };

        private static SpacePacket CreateTm325(byte[] body, byte messageCounter = 7)
        {
            var packetTime = TimeEpoch + TimeSpan.FromSeconds(120.25);
            return PusPacket.CreateTm(
                TimeEpoch, apId: 0x123, sequenceCount: (ushort)messageCounter,
                serviceType: 3, serviceSubtype: 25, messageCounter, destinationId: 0x0004,
                packetTime, body);
        }

        [Fact]
        public void DecodeTm325_ProducesMeasurements()
        {
            var body = new List<byte>();
            foreach (var definition in Definitions)
            {
                body.Add(definition.Id);
                body.AddRange(PusHousekeepingDecoder.EncodeValue(PhysicalValue(definition), definition));
            }

            var pus = PusPacket.Parse(CreateTm325(body.ToArray()).ToBytes(), TimeEpoch);
            var decoder = new PusHousekeepingDecoder(Definitions);
            var report = decoder.Decode(pus);

            Assert.Null(report.SID);
            Assert.Equal(4, report.Measurements.Count);

            var voltage = report.Measurements[0];
            Assert.Equal(12.50, voltage.Value, 3);
            Assert.Equal(ElectricPotentialUnit.Volt, voltage.Unit);
            Assert.Equal(Quality.Good, voltage.Quality);
            Assert.Equal(pus.Time!.Value, voltage.Timestamp);
            Assert.Equal("0x123", voltage.Metadata["apid"]);
            Assert.Equal("3", voltage.Metadata["service_type"]);
            Assert.Equal("25", voltage.Metadata["service_subtype"]);
            Assert.Equal("7", voltage.Metadata["message_counter"]);
            Assert.Equal("BatteryVoltage", voltage.Metadata["pus_parameter"]);

            Assert.Equal(25.0, report.Measurements[1].Value, 3);
            Assert.Equal(TemperatureUnit.DegreeCelsius, report.Measurements[1].Unit);
            Assert.Equal(-3.2, report.Measurements[2].Value, 6);
            Assert.Equal(ElectricCurrentUnit.Ampere, report.Measurements[2].Unit);
            Assert.Equal(1.0, report.Measurements[3].Value, 6);
        }

        [Fact]
        public void Decode_OutOfRange_MarksBad()
        {
            var temp = Definitions[1];
            var body = new List<byte> { temp.Id };
            body.AddRange(PusHousekeepingDecoder.EncodeValue(200.0, temp)); // 200 °C > max 100
            var pus = PusPacket.Parse(CreateTm325(body.ToArray()).ToBytes(), TimeEpoch);
            var decoder = new PusHousekeepingDecoder(Definitions);

            var report = decoder.Decode(pus);

            Assert.Single(report.Measurements);
            Assert.Equal(200.0, report.Measurements[0].Value, 3);
            Assert.Equal(Quality.Bad, report.Measurements[0].Quality);
        }

        [Fact]
        public void Decode_SidQualifiedVariant()
        {
            var body = new List<byte> { 0x00, 0x2A, Definitions[1].Id };
            body.AddRange(PusHousekeepingDecoder.EncodeValue(25.0, Definitions[1]));

            var pus = PusPacket.Parse(CreateTm325(body.ToArray()).ToBytes(), TimeEpoch);
            var decoder = new PusHousekeepingDecoder(Definitions);

            var report = decoder.Decode(pus, sidIncluded: true);

            Assert.Equal((ushort)0x2A, report.SID!.Value);
            Assert.Single(report.Measurements);
            Assert.Equal("42", report.Measurements[0].Metadata["sid"]);
        }

        [Fact]
        public void Decode_UnknownParameterId_Throws()
        {
            var pus = PusPacket.Parse(CreateTm325(new byte[] { 0xE0, 0x00 }).ToBytes(), TimeEpoch);
            var decoder = new PusHousekeepingDecoder(Definitions);

            Assert.Throws<System.InvalidOperationException>(() => decoder.Decode(pus));
        }

        [Fact]
        public void Decode_TruncatedValue_Throws()
        {
            // UInt16 parameter 0x01 declares 2 value octets but only 1 is present.
            var pus = PusPacket.Parse(CreateTm325(new byte[] { 0x01, 0x12 }).ToBytes(), TimeEpoch);
            var decoder = new PusHousekeepingDecoder(Definitions);

            Assert.Throws<System.InvalidOperationException>(() => decoder.Decode(pus));
        }

        [Fact]
        public void Decoder_DuplicateIds_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new PusHousekeepingDecoder(new[] { Definitions[0], Definitions[0] }));
        }

        [Fact]
        public void EncodeValue_Float64RoundTrips()
        {
            var definition = Definitions[2];
            var raw = PusHousekeepingDecoder.EncodeValue(-3.2, definition);
            Assert.Equal(8, raw.Length);

            var body = new List<byte> { definition.Id };
            body.AddRange(raw);
            var pus = PusPacket.Parse(CreateTm325(body.ToArray()).ToBytes(), TimeEpoch);
            var report = new PusHousekeepingDecoder(Definitions).Decode(pus);

            Assert.Equal(-3.2, report.Measurements[0].Value, 6);
        }

        private static double PhysicalValue(PusParameterDefinition definition) =>
            definition.Id switch
            {
                0x01 => 12.50,
                0x02 => 25.0,
                0x03 => -3.2,
                0x04 => 1,
                _ => 0,
            };
    }
}