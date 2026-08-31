using System;
using SignalFlux;

namespace SignalFlux.Protocols.Arinc429
{
    /// <summary>
    /// Extension helpers for ARINC 429 data words: BNR (binary) value conversion, SSM interpretation,
    /// and conversion into SignalFlux <see cref="Measurement{T}"/> values.
    /// </summary>
    public static class Arinc429Extensions
    {
        /// <summary>
        /// Decodes a Binary (BNR) data field into a physical double. The 19-bit data field (bits 11–29)
        /// holds the value as a two's-complement integer scaled by <paramref name="lsbWeight"/>.
        /// </summary>
        /// <param name="word">The ARINC word.</param>
        /// <param name="lsbWeight">The weight of the least significant bit of the data field.</param>
        /// <returns>The BNR value, or NaN when the data field is an error/not-available state.</returns>
        public static double DecodeBnr(this Arinc429Word word, double lsbWeight = 1.0)
        {
            uint data = word.Data;
            // Bit 18 of the data field (ARINC bit 29) is the sign bit for BNR.
            long signed;
            if ((data & (1u << 18)) != 0)
                signed = unchecked((long)(int)(data | ~0x7FFFFu));
            else
                signed = (long)data;
            return signed * lsbWeight;
        }

        /// <summary>
        /// Encodes a physical value into the 19-bit BNR data field (as two's complement over the full
        /// data range) and returns a new word preserving label/SDI/SSM.
        /// </summary>
        public static Arinc429Word EncodeBnr(this Arinc429Word word, double value, double lsbWeight = 1.0)
        {
            long raw = (long)Math.Round(value / lsbWeight, 0, MidpointRounding.AwayFromZero);
            uint data = unchecked((uint)raw) & 0x7FFFF;
            return new Arinc429Word(word.Label, word.Sdi, data, word.Ssm, word.Parity);
        }

        /// <summary>Interpretation of the 2-bit Sign/Status Matrix (SSM).</summary>
        public enum ArincSsm
        {
            /// <summary>Normal operation (numerical word), failure condition (BCD word).</summary>
            NormalOrFailure = 0,
            /// <summary>Functional test (numerical word), no computed data (BCD word).</summary>
            FunctionalTestOrNoData = 1,
            /// <summary>Reserved / "res" state.</summary>
            Reserved = 2,
            /// <summary>Failure warning (numerical word), reserved (BCD word).</summary>
            FailureWarningOrReserved = 3,
        }

        /// <summary>Interprets the SSM field as an <see cref="ArincSsm"/>.</summary>
        public static ArincSsm SsmInterpretation(this Arinc429Word word) => (ArincSsm)word.Ssm;

        /// <summary>
        /// Converts the decoded BNR physical value into a <see cref="Measurement{T}"/>, using the SSM to
        /// drive the quality (non-normal SSM states map to <see cref="Quality.Bad"/>).
        /// </summary>
        /// <param name="word">The ARINC word.</param>
        /// <param name="lsbWeight">The BNR LSB weight.</param>
        /// <param name="unit">Optional engineering unit.</param>
        /// <param name="source">Optional source label stored in metadata.</param>
        public static Measurement<double> ToBnrMeasurement(
            this Arinc429Word word, double lsbWeight = 1.0, Enum unit = null, string source = "arinc429")
        {
            double value = word.DecodeBnr(lsbWeight);
            Quality quality = word.Ssm == 0 ? Quality.Good : Quality.Bad;

            var metadata = new Metadata()
                .With("source", source)
                .With("label", string.Format("0o{0:X2}", word.Label))
                .With("sdi", word.Sdi.ToString())
                .With("ssm", word.Ssm.ToString());

            return new Measurement<double>(value, Timestamp.Now, unit, quality, metadata);
        }
    }
}
