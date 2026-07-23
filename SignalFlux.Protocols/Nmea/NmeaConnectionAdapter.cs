using System;
using System.Threading;
using System.Threading.Tasks;
using NmeaParser;

namespace SignalFlux.Protocols.Nmea
{
    /// <summary>Wraps a <see cref="NmeaDevice"/> to provide an observable stream of <see cref="Measurement{T}"/> values from NMEA sentences.</summary>
    public sealed class NmeaConnectionAdapter : IAsyncDisposable
    {
        private readonly NmeaDevice _device;
        private bool _disposed;

        /// <summary>Creates an adapter wrapping the specified NMEA device.</summary>
        /// <param name="device">The SharpGIS NmeaParser <see cref="NmeaDevice"/> instance to wrap.</param>
        public NmeaConnectionAdapter(NmeaDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <summary>Subscribes to a specific NMEA message type and field, invoking the handler with each extracted <see cref="Measurement{T}"/>.</summary>
        /// <param name="messageType">The NMEA message type to filter (e.g., "RMC", "GGA", "VTG", "GSA", "GSV", "GLL").</param>
        /// <param name="fieldName">The field name to extract from the message.</param>
        /// <param name="handler">The callback invoked with each extracted measurement.</param>
        /// <param name="cancellationToken">Cancellation token to stop observing.</param>
        /// <returns>A <see cref="Task"/> that completes when the cancellation token is triggered.</returns>
        public async Task ObserveMeasurementAsync(
            string messageType,
            string fieldName,
            Action<Measurement<double>> handler,
            CancellationToken cancellationToken = default)
        {
            _device.MessageReceived += (sender, args) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                Measurement<double>? measurement = messageType.ToUpperInvariant() switch
                {
                    "RMC" when args.Message is NmeaParser.Messages.Rmc rmc => rmc.ToMeasurement(fieldName),
                    "GGA" when args.Message is NmeaParser.Messages.Gga gga => gga.ToMeasurement(fieldName),
                    "VTG" when args.Message is NmeaParser.Messages.Vtg vtg => vtg.ToMeasurement(fieldName),
                    "GSA" when args.Message is NmeaParser.Messages.Gsa gsa => gsa.ToMeasurement(fieldName),
                    "GSV" when args.Message is NmeaParser.Messages.Gsv gsv => gsv.ToMeasurement(fieldName),
                    "GLL" when args.Message is NmeaParser.Messages.Gll gll => gll.ToMeasurement(fieldName),
                    _ => (Measurement<double>?)null
                };

                if (measurement.HasValue)
                    handler(measurement.Value);
            };

            await _device.OpenAsync().ConfigureAwait(false);

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }
        }

        /// <summary>Disposes the adapter and releases the underlying NMEA device.</summary>
        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _device.Dispose();
            }
            return default;
        }
    }
}
