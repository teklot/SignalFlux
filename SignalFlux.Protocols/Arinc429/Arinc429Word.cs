using System;

namespace SignalFlux.Protocols.Arinc429
{
    /// <summary>
    /// A 32-bit ARINC 429 data word. Bits are numbered ARINC-style: bit 1 is the most significant
    /// (and is transmitted first), bit 32 is the least significant (transmitted last). When represented as a
    /// <see cref="uint"/>, bit 1 maps to bit 31 and bit 32 maps to bit 0.
    /// </summary>
    public readonly struct Arinc429Word : IEquatable<Arinc429Word>
    {
        /// <summary>The raw 32-bit word. Bit 31 = ARINC bit 1 (label MSB), bit 0 = ARINC bit 32 (parity).</summary>
        public uint Word { get; }

        /// <summary>Creates a word from its raw 32-bit representation.</summary>
        public Arinc429Word(uint word) => Word = word;

        /// <summary>
        /// Builds an ARINC 429 word from its logical fields, with the ARINC bit numbering applied.
        /// </summary>
        /// <param name="label">The 8-bit label (bits 1–8).</param>
        /// <param name="sdi">The 2-bit Source/Destination Identifier (bits 9–10).</param>
        /// <param name="data">The 19-bit data field (bits 11–29).</param>
        /// <param name="ssm">The 2-bit Sign/Status Matrix (bits 30–31).</param>
        /// <param name="parity">Parity bit (bit 32); 0 or 1.</param>
        public Arinc429Word(byte label, byte sdi, uint data, byte ssm, byte parity)
        {
            if (data > 0x7FFFF) throw new ArgumentOutOfRangeException(nameof(data), "Data must fit in 19 bits.");
            if (sdi > 0x3) throw new ArgumentOutOfRangeException(nameof(sdi));
            if (ssm > 0x3) throw new ArgumentOutOfRangeException(nameof(ssm));
            if (parity > 0x1) throw new ArgumentOutOfRangeException(nameof(parity));

            Word = ((uint)label << 24)
                 | ((uint)sdi << 22)
                 | ((uint)data << 3)
                 | ((uint)ssm << 1)
                 | (uint)parity;
        }

        /// <summary>The 8-bit label (ARINC bits 1–8, the most significant octet).</summary>
        public byte Label => (byte)(Word >> 24);

        /// <summary>The 2-bit Source/Destination Identifier (ARINC bits 9–10).</summary>
        public byte Sdi => (byte)((Word >> 22) & 0x3);

        /// <summary>The 19-bit data field (ARINC bits 11–29).</summary>
        public uint Data => (Word >> 3) & 0x7FFFF;

        /// <summary>The 2-bit Sign/Status Matrix (ARINC bits 30–31).</summary>
        public byte Ssm => (byte)((Word >> 1) & 0x3);

        /// <summary>The parity bit (ARINC bit 32).</summary>
        public byte Parity => (byte)(Word & 0x1);

        /// <summary>
        /// Returns true when the word has odd parity (an even number of set bits in bits 1–31 plus
        /// the parity bit); ARINC 429 defines odd parity for data words.
        /// </summary>
        public bool HasOddParity
        {
            get
            {
                int count = 0;
                uint w = Word;
                while (w != 0)
                {
                    count += (int)(w & 1);
                    w >>= 1;
                }
                return (count & 1) == 1;
            }
        }

        /// <summary>Returns a copy with the parity bit set to produce odd parity over the whole word.</summary>
        public Arinc429Word WithOddParity() =>
            new Arinc429Word(HasOddParity ? Word : (Word ^ 0x1));

        /// <summary>Returns a copy with the parity bit set to produce even parity over the whole word.</summary>
        public Arinc429Word WithEvenParity() =>
            new Arinc429Word(HasOddParity ? (Word ^ 0x1) : Word);

        /// <inheritdoc/>
        public bool Equals(Arinc429Word other) => Word == other.Word;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Arinc429Word other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Word.GetHashCode();

        /// <summary>Returns true when two words are equal.</summary>
        public static bool operator ==(Arinc429Word left, Arinc429Word right) => left.Equals(right);

        /// <summary>Returns true when two words are not equal.</summary>
        public static bool operator !=(Arinc429Word left, Arinc429Word right) => !left.Equals(right);

        /// <summary>Returns the word formatted as an 8-digit hexadecimal value.</summary>
        public override string ToString() => string.Format("0x{0:X8}", Word);
    }
}
