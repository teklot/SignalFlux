using System;
using System.Collections.Generic;
using System.Globalization;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>Parses and encodes IEEE-488.2 binary block data as used by SCPI instruments.</summary>
    public static class ScpiBlockParser
    {
        /// <summary>Parses a complete response buffer containing an IEEE-488.2 binary block header followed by its payload.</summary>
        /// <param name="response">The raw response bytes, which may include any leading white space and a trailing line terminator.</param>
        /// <returns>The parsed block.</returns>
        /// <exception cref="FormatException">Thrown when the response does not contain a valid IEEE-488.2 block header.</exception>
        public static ScpiBlock Parse(byte[] response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            int index = 0;
            return ParseFromIndex(response, ref index);
        }

        /// <summary>Parses an IEEE-488.2 binary block from a pull-based byte source.</summary>
        /// <param name="readByte">Function returning the next byte value (0-255), or -1 when the source is exhausted.</param>
        /// <returns>The parsed block.</returns>
        /// <exception cref="FormatException">Thrown when the byte source does not produce a valid IEEE-488.2 block header.</exception>
        public static ScpiBlock ParseFromReader(Func<int> readByte)
        {
            if (readByte == null) throw new ArgumentNullException(nameof(readByte));
            int hash = readByte();
            if (hash != '#')
                throw new FormatException("Expected IEEE-488.2 block header starting with '#'.");
            int header = readByte();
            if (header < '0' || header > '9')
                throw new FormatException("Expected a length-code digit after '#' in IEEE-488.2 block header.");
            int digitCount = header - '0';
            if (digitCount == 0)
            {
                var indefinite = new List<byte>();
                int value;
                while ((value = readByte()) != -1)
                    indefinite.Add((byte)value);
                return new ScpiBlock(indefinite.ToArray());
            }
            long length = 0;
            for (int i = 0; i < digitCount; i++)
            {
                int digit = readByte();
                if (digit < '0' || digit > '9')
                    throw new FormatException("Invalid digit in IEEE-488.2 block length field.");
                length = length * 10 + (digit - '0');
            }
            var data = new byte[length];
            int offset = 0;
            int valueRead;
            while (offset < length && (valueRead = readByte()) != -1)
                data[offset++] = (byte)valueRead;
            if (offset < length)
                throw new FormatException("IEEE-488.2 block ended before the declared length was satisfied.");
            return new ScpiBlock(data);
        }

        /// <summary>Encodes the given sample values as a definite-length IEEE-488.2 binary block of big-endian IEEE-754 double-precision values.</summary>
        /// <param name="samples">The sample values to encode.</param>
        /// <returns>The encoded block payload, including the block header.</returns>
        public static byte[] Encode(double[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var payload = new byte[samples.Length * 8];
            for (int i = 0; i < samples.Length; i++)
            {
                ulong bits = (ulong)BitConverter.DoubleToInt64Bits(samples[i]);
                for (int b = 0; b < 8; b++)
                    payload[i * 8 + b] = (byte)((bits >> (56 - b * 8)) & 0xFF);
            }
            string length = payload.Length.ToString(CultureInfo.InvariantCulture);
            var result = new byte[1 + 1 + length.Length + payload.Length];
            result[0] = (byte)'#';
            result[1] = (byte)('0' + length.Length);
            for (int i = 0; i < length.Length; i++)
                result[2 + i] = (byte)length[i];
            Array.Copy(payload, 0, result, 2 + length.Length, payload.Length);
            return result;
        }

        private static ScpiBlock ParseFromIndex(byte[] response, ref int index)
        {
            while (index < response.Length && (response[index] == ' ' || response[index] == '\r' || response[index] == '\n'))
                index++;
            if (index >= response.Length || response[index] != '#')
                throw new FormatException("Expected IEEE-488.2 block header starting with '#'.");
            index++;
            if (index >= response.Length)
                throw new FormatException("Expected a length-code digit after '#' in IEEE-488.2 block header.");
            int header = response[index++];
            if (header < '0' || header > '9')
                throw new FormatException("Expected a length-code digit after '#' in IEEE-488.2 block header.");
            int digitCount = header - '0';
            if (digitCount == 0)
            {
                var indefinite = new List<byte>();
                while (index < response.Length)
                    indefinite.Add(response[index++]);
                return new ScpiBlock(indefinite.ToArray());
            }
            long length = 0;
            for (int i = 0; i < digitCount; i++)
            {
                if (index >= response.Length)
                    throw new FormatException("IEEE-488.2 block length field truncated.");
                int digit = response[index++];
                if (digit < '0' || digit > '9')
                    throw new FormatException("Invalid digit in IEEE-488.2 block length field.");
                length = length * 10 + (digit - '0');
            }
            if (index + length > response.Length)
                throw new FormatException("IEEE-488.2 block payload shorter than the declared length.");
            var data = new byte[length];
            Array.Copy(response, index, data, 0, length);
            return new ScpiBlock(data);
        }
    }
}