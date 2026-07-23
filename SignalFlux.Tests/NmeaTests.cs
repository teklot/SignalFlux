using System;
using NmeaParser;
using NmeaParser.Messages;
using Xunit;
using SignalFlux.Protocols.Nmea;

namespace SignalFlux.Tests
{
    public class NmeaTests
    {
        private static Rmc ParseRmc(string sentence)
        {
            var msg = NmeaMessage.Parse(sentence, null, true);
            Assert.NotNull(msg);
            Assert.IsType<Rmc>(msg);
            return (Rmc)msg;
        }

        private static Gga ParseGga(string sentence)
        {
            var msg = NmeaMessage.Parse(sentence, null, true);
            Assert.NotNull(msg);
            Assert.IsType<Gga>(msg);
            return (Gga)msg;
        }

        private static Vtg ParseVtg(string sentence)
        {
            var msg = NmeaMessage.Parse(sentence, null, true);
            Assert.NotNull(msg);
            Assert.IsType<Vtg>(msg);
            return (Vtg)msg;
        }

        private static Gsa ParseGsa(string sentence)
        {
            var msg = NmeaMessage.Parse(sentence, null, true);
            Assert.NotNull(msg);
            Assert.IsType<Gsa>(msg);
            return (Gsa)msg;
        }

        [Fact]
        public void Rmc_Latitude_ReturnsCorrectValue()
        {
            var rmc = ParseRmc("$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,0.0,0.0,070426,,,A*19");
            var m = rmc.ToMeasurement("latitude");

            Assert.Equal(47.6629, m.Value, 3);
        }

        [Fact]
        public void Rmc_Longitude_ReturnsCorrectValue()
        {
            var rmc = ParseRmc("$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,0.0,0.0,070426,,,A*19");
            var m = rmc.ToMeasurement("longitude");

            Assert.Equal(8.9093, m.Value, 3);
        }

        [Fact]
        public void Rmc_Speed_ReturnsCorrectValue()
        {
            var rmc = ParseRmc("$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,5.2,45.0,070426,,,A*19");
            var m = rmc.ToMeasurement("speed");

            Assert.Equal(5.2, m.Value, 4);
        }

        [Fact]
        public void Rmc_Course_ReturnsCorrectValue()
        {
            var rmc = ParseRmc("$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,5.2,45.0,070426,,,A*19");
            var m = rmc.ToMeasurement("course");

            Assert.Equal(45.0, m.Value, 4);
        }

        [Fact]
        public void Rmc_InvalidField_ThrowsArgumentException()
        {
            var rmc = ParseRmc("$GPRMC,201530.00,A,4739.77420,N,00854.55940,E,0.0,0.0,070426,,,A*19");
            Assert.Throws<ArgumentException>(() => rmc.ToMeasurement("altitude"));
        }

        [Fact]
        public void Gga_Altitude_ReturnsCorrectValue()
        {
            var gga = ParseGga("$GPGGA,201530.00,4739.77420,N,00854.55940,E,1,08,1.0,488.0,M,47.0,M,,*42");
            var m = gga.ToMeasurement("altitude");

            Assert.Equal(488.0, m.Value, 4);
        }

        [Fact]
        public void Gga_Satellites_ReturnsCorrectCount()
        {
            var gga = ParseGga("$GPGGA,201530.00,4739.77420,N,00854.55940,E,1,08,1.0,488.0,M,47.0,M,,*42");
            var m = gga.ToMeasurement("satellites");

            Assert.Equal(8.0, m.Value, 4);
        }

        [Fact]
        public void Gga_Hdop_ReturnsCorrectValue()
        {
            var gga = ParseGga("$GPGGA,201530.00,4739.77420,N,00854.55940,E,1,08,1.0,488.0,M,47.0,M,,*42");
            var m = gga.ToMeasurement("hdop");

            Assert.Equal(1.0, m.Value, 4);
        }

        [Fact]
        public void Vtg_Course_ReturnsValue()
        {
            var vtg = ParseVtg("$GPVTG,45.0,T,43.5,M,5.2,N,9.6,K,A*16");
            var m = vtg.ToMeasurement("course");

            Assert.Equal(45.0, m.Value, 4);
        }

        [Fact]
        public void Vtg_SpeedKph_ReturnsCorrectValue()
        {
            var vtg = ParseVtg("$GPVTG,45.0,T,43.5,M,5.2,N,9.6,K,A*16");
            var m = vtg.ToMeasurement("speed-kph");

            Assert.Equal(9.6, m.Value, 4);
        }

        [Fact]
        public void Vtg_SpeedKnots_ReturnsCorrectValue()
        {
            var vtg = ParseVtg("$GPVTG,45.0,T,43.5,M,5.2,N,9.6,K,A*16");
            var m = vtg.ToMeasurement("speed-knots");

            Assert.Equal(5.2, m.Value, 4);
        }

        [Fact]
        public void Gsa_Pdop_ReturnsCorrectValue()
        {
            var gsa = ParseGsa("$GPGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.5,0.9,1.2*12");
            var m = gsa.ToMeasurement("pdop");

            Assert.Equal(1.5, m.Value, 4);
        }

        [Fact]
        public void Gsa_Hdop_ReturnsCorrectValue()
        {
            var gsa = ParseGsa("$GPGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.5,0.9,1.2*12");
            var m = gsa.ToMeasurement("hdop");

            Assert.Equal(0.9, m.Value, 4);
        }

        [Fact]
        public void Gsa_Vdop_ReturnsCorrectValue()
        {
            var gsa = ParseGsa("$GPGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.5,0.9,1.2*12");
            var m = gsa.ToMeasurement("vdop");

            Assert.Equal(1.2, m.Value, 4);
        }

        [Fact]
        public void Gga_Latitude_ReturnsCorrectValue()
        {
            var gga = ParseGga("$GPGGA,201530.00,4739.77420,N,00854.55940,E,1,08,1.0,488.0,M,47.0,M,,*42");
            var m = gga.ToMeasurement("latitude");

            Assert.Equal(47.6629, m.Value, 3);
        }
    }
}
