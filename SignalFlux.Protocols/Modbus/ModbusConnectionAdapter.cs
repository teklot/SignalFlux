using System;
using System.Threading.Tasks;
using NModbus;

namespace SignalFlux.Protocols.Modbus
{
    /// <summary>Wraps an NModbus <see cref="IModbusMaster"/> to read and write <see cref="Signal{T}"/> values as Modbus registers.</summary>
    public sealed class ModbusConnectionAdapter : IDisposable
    {
        private readonly IModbusMaster _master;
        private bool _disposed;

        /// <summary>Creates an adapter wrapping the specified Modbus master.</summary>
        /// <param name="master">The NModbus <see cref="IModbusMaster"/> instance to wrap.</param>
        public ModbusConnectionAdapter(IModbusMaster master)
        {
            _master = master ?? throw new ArgumentNullException(nameof(master));
        }

        /// <summary>Reads holding registers and reconstructs a <see cref="Signal{T}"/>.</summary>
        /// <param name="slaveId">Modbus slave device ID.</param>
        /// <param name="startAddress">Starting register address.</param>
        /// <param name="count">Number of registers to read.</param>
        /// <param name="frequency">Signal frequency in Hz for the reconstructed signal.</param>
        /// <param name="startTime">Start timestamp of the signal.</param>
        /// <param name="scale">Scale factor applied during decoding (default 1.0).</param>
        /// <param name="offset">Offset added after reverse scaling (default 0.0).</param>
        /// <param name="source">Source identifier for the reconstructed signal.</param>
        /// <returns>A <see cref="Signal{T}"/> reconstructed from the read registers.</returns>
        public async Task<Signal<double>> ReadSignalAsync(
            byte slaveId,
            ushort startAddress,
            ushort count,
            double frequency,
            Timestamp startTime,
            double scale = 1.0,
            double offset = 0.0,
            string source = "modbus")
        {
            var registers = await _master.ReadHoldingRegistersAsync(slaveId, startAddress, count).ConfigureAwait(false);
            return registers.ToSignal(frequency, startTime, scale, offset, source);
        }

        /// <summary>Writes a <see cref="Signal{T}"/> to holding registers with scale and offset.</summary>
        /// <param name="signal">The signal to encode and write.</param>
        /// <param name="slaveId">Modbus slave device ID.</param>
        /// <param name="startAddress">Starting register address.</param>
        /// <param name="scale">Scale factor applied before encoding (default 1.0).</param>
        /// <param name="offset">Offset subtracted after scaling (default 0.0).</param>
        public async Task WriteSignalAsync(
            Signal<double> signal,
            byte slaveId,
            ushort startAddress,
            double scale = 1.0,
            double offset = 0.0)
        {
            var registers = signal.ToModbusRegisters(scale, offset);
            await _master.WriteMultipleRegistersAsync(slaveId, startAddress, registers).ConfigureAwait(false);
        }

        /// <summary>Disposes the adapter and releases the underlying Modbus master.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _master.Dispose();
            }
        }
    }
}
