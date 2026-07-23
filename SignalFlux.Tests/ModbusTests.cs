using System;
using Xunit;
using SignalFlux.Protocols.Modbus;

namespace SignalFlux.Tests
{
    public class ModbusTests
    {
        [Fact]
        public void ToModbusRegisters_ScaleAndOffset_EncodesCorrectly()
        {
            var signal = new Signal<double>(new double[] { 10.0, 20.0, 30.0 }, 10, Timestamp.Zero);
            var registers = signal.ToModbusRegisters(scale: 100.0, offset: 0.0);

            Assert.Equal(3, registers.Length);
            Assert.Equal((ushort)1000, registers[0]);
            Assert.Equal((ushort)2000, registers[1]);
            Assert.Equal((ushort)3000, registers[2]);
        }

        [Fact]
        public void ToModbusRegisters_ClampsToMax()
        {
            var signal = new Signal<double>(new double[] { 1000.0 }, 10, Timestamp.Zero);
            var registers = signal.ToModbusRegisters(scale: 1.0, maxValue: 255);

            Assert.Equal((ushort)255, registers[0]);
        }

        [Fact]
        public void ToModbusRegisters_ClampsToMin()
        {
            var signal = new Signal<double>(new double[] { -5.0 }, 10, Timestamp.Zero);
            var registers = signal.ToModbusRegisters(scale: 1.0, minValue: 0);

            Assert.Equal((ushort)0, registers[0]);
        }

        [Fact]
        public void ToSignal_DecodesRegistersBackToSignal()
        {
            var registers = new ushort[] { 1000, 2000, 3000 };
            var signal = registers.ToSignal(frequency: 10.0, startTime: Timestamp.Zero, scale: 100.0, offset: 0.0);

            Assert.Equal(3, signal.Count);
            Assert.Equal(10.0, signal.Frequency);
            Assert.Equal(10.0, signal.Samples.ToArray()[0], 4);
            Assert.Equal(20.0, signal.Samples.ToArray()[1], 4);
            Assert.Equal(30.0, signal.Samples.ToArray()[2], 4);
        }

        [Fact]
        public void RoundTrip_EncodeDecode_PreservesValues()
        {
            var original = new Signal<double>(new double[] { 1.5, 2.5, 3.5 }, 50, Timestamp.Zero);
            var registers = original.ToModbusRegisters(scale: 100.0);
            var decoded = registers.ToSignal(50.0, Timestamp.Zero, scale: 100.0);

            var originalSamples = original.Samples.ToArray();
            var decodedSamples = decoded.Samples.ToArray();
            for (int i = 0; i < originalSamples.Length; i++)
            {
                Assert.Equal(originalSamples[i], decodedSamples[i], 4);
            }
        }

        [Fact]
        public void ToModbusRegisters_DefaultScale_ReturnsSameValues()
        {
            var signal = new Signal<double>(new double[] { 42.0 }, 10, Timestamp.Zero);
            var registers = signal.ToModbusRegisters();

            Assert.Equal((ushort)42, registers[0]);
        }
    }
}
