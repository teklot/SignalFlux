using System;
using System.Collections.Generic;
using SignalFlux;
using SignalFlux.Space.Ccsds;
using SignalFlux.Space.Pus;
using UnitsNet.Units;
using static System.Console;

namespace SignalFlux.Console
{
    /// <summary>
    /// v0.9 space telemetry demo: builds a synthetic ECSS PUS housekeeping packet
    /// (CCSDS Space Packet framing, CUC time, Service 3 TM[3,25]), CRCs it, and decodes it back
    /// into <see cref="Measurement{T}"/> values.
    /// </summary>
    public static class SpaceTelemetrySamples
    {
        public static void RunSpaceTelemetrySample()
        {
            WriteLine("=== Space Telemetry Demo (CCSDS / ECSS PUS, v0.9) ===");

            var epoch = Timestamp.FromUnixSeconds(0);
            var packetTime = Timestamp.UtcNow;

            var definitions = new PusParameterDefinition[]
            {
                new(0x01, "BatteryVoltage", PusParameterType.UInt16, ElectricPotentialUnit.Volt, scale: 0.01, minimum: 8.0, maximum: 16.0),
                new(0x02, "StarTrackerTemp", PusParameterType.Int16, TemperatureUnit.DegreeCelsius, scale: 0.1, minimum: -20, maximum: 60),
                new(0x03, "ReactionWheelRpm", PusParameterType.Int16, RotationalSpeedUnit.RadianPerSecond, scale: 0.01),
                new(0x04, "PowerOnFlag", PusParameterType.UInt8),
            };

            var body = new List<byte>();
            AddParameter(body, 0x01, 12.50, definitions[0]);
            AddParameter(body, 0x02, 25.0, definitions[1]);
            AddParameter(body, 0x03, -1.20, definitions[2]);
            AddParameter(body, 0x04, 1, definitions[3]);

            var spacePacket = PusPacket.CreateTm(
                epoch,
                apId: 0x123,
                sequenceCount: 0x0A,
                serviceType: 3,
                serviceSubtype: 25,
                messageCounter: 0x07,
                destinationId: 0x0004,
                packetTime,
                body.ToArray());

            byte[] raw = spacePacket.ToBytes();
            ushort crc = Crc16.Compute(raw);

            WriteLine($"Packet: {raw.Length} octets, APID 0x123, TM[3,25], CRC-16 0x{crc:X4} verified: {Crc16.Verify(raw, crc)}");
            WriteLine($"Time (CUC): {packetTime.DateTime:O}");

            var pus = PusPacket.Parse(raw, epoch);
            var report = new PusHousekeepingDecoder(definitions).Decode(pus);

            WriteLine("Housekeeping parameters:");
            foreach (var m in report.Measurements)
                WriteLine($"  {m.Metadata["pus_parameter"]}: {m.Value} {m.Unit} [{m.Quality}]");
        }

        private static void AddParameter(List<byte> body, byte id, double value, PusParameterDefinition definition)
        {
            body.Add(id);
            body.AddRange(PusHousekeepingDecoder.EncodeValue(value, definition));
        }
    }
}