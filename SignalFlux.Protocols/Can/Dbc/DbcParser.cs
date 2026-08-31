using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SignalFlux.Protocols.Can;

namespace SignalFlux.Protocols.Can.Dbc
{
    /// <summary>
    /// Parses CAN DBC (CANdb++) database files into <see cref="DbcDatabase"/> models. Supports the
    /// core <c>BO_</c> (message), <c>SG_</c> (signal), <c>VAL_</c> (value table), and <c>CM_</c>
    /// (comment) sections. The parser is tolerant of common whitespace and line-continuation conventions.
    /// </summary>
    public static class DbcParser
    {
        /// <summary>Parses a DBC database from a string.</summary>
        /// <param name="dbcContent">The DBC file content.</param>
        /// <returns>A populated <see cref="DbcDatabase"/>.</returns>
        public static DbcDatabase Parse(string dbcContent)
        {
            if (dbcContent == null) throw new ArgumentNullException(nameof(dbcContent));
            return ParseLines(dbcContent.Replace("\r\n", "\n").Split('\n'));
        }

        /// <summary>Parses a DBC database from a file path.</summary>
        /// <param name="filePath">Path to the .dbc file.</param>
        /// <returns>A populated <see cref="DbcDatabase"/>.</returns>
        public static DbcDatabase ParseFile(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            return Parse(File.ReadAllText(filePath));
        }

        private static DbcDatabase ParseLines(string[] lines)
        {
            var database = new DbcDatabase();
            DbcMessage currentMessage = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("VERSION", StringComparison.Ordinal) ||
                    line.StartsWith("NS_", StringComparison.Ordinal) ||
                    line.StartsWith("BS_", StringComparison.Ordinal) ||
                    line.StartsWith("BU_", StringComparison.Ordinal) ||
                    line.StartsWith("BA_DEF", StringComparison.Ordinal) ||
                    line.StartsWith("BA_", StringComparison.Ordinal) ||
                    line.StartsWith("SIG_VALTYPE_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("BO_ ", StringComparison.Ordinal))
                {
                    currentMessage = ParseMessage(line);
                    if (currentMessage != null)
                        database.Messages[currentMessage.Id] = currentMessage;
                    continue;
                }

                if (line.StartsWith("SG_ ", StringComparison.Ordinal))
                {
                    if (currentMessage != null)
                    {
                        var signal = ParseSignal(line);
                        if (signal != null && !currentMessage.Signals.ContainsKey(signal.Name))
                            currentMessage.Signals[signal.Name] = signal;
                    }
                    continue;
                }

                if (line.StartsWith("VAL_ ", StringComparison.Ordinal))
                {
                    ParseValueTable(line, database);
                    continue;
                }

                if (line.StartsWith("CM_ ", StringComparison.Ordinal))
                {
                    ParseComment(line, database);
                    continue;
                }
            }

            return database;
        }

        private static DbcMessage ParseMessage(string line)
        {
            // BO_ <id> <name>: <length> <transmitter>
            var tokens = SplitTopLevel(line.Substring(4).Trim());
            if (tokens.Count < 3) return null;

            if (!uint.TryParse(tokens[0], out uint id)) return null;
            string name = StripQuotes(tokens[1]).TrimEnd(':').Trim();
            int colonIndex = tokens[2].IndexOf(':');
            int length = int.TryParse(colonIndex >= 0 ? tokens[2].Substring(0, colonIndex) : tokens[2],
                NumberStyles.Any, CultureInfo.InvariantCulture, out int len)
                ? len
                : 8;

            return new DbcMessage
            {
                Id = id,
                Name = name,
                Length = length,
                Transmitter = tokens.Count > 3 ? StripQuotes(tokens[3]) : string.Empty,
            };
        }

        private static DbcSignal ParseSignal(string line)
        {
            // SG_ <name> <mux>: <start>|<length>@<byteorder><signed> (<factor>,<offset>) [<min>|<max>] "<unit>" <receivers>
            string body = line.Substring(4).Trim();
            var tokens = SplitTopLevel(body, new[] { ':' }, trim: false);

            if (tokens.Count != 2) return null;
            string namePart = tokens[0].Trim();
            string layoutPart = tokens[1];

            // Split name part into name + optional mux indicator
            var nameTokens = SplitTopLevel(namePart);
            string name = StripQuotes(nameTokens[0]);
            string mux = nameTokens.Count > 1 ? StripQuotes(nameTokens[1]) : null;

            var layoutTokens = SplitTopLevel(layoutPart);
            if (layoutTokens.Count < 3) return null;

            // layoutTokens[0] = start|length, layoutTokens[1] = (factor,offset), layoutTokens[2] = [min|max]
            var layout = SplitTopLevel(layoutTokens[0], new[] { '|' });
            if (layout.Count != 2) return null;

            if (!int.TryParse(layout[0], out int startBit)) return null;

            string rawFormat = string.Empty;
            int length = 0;

            // The '@' and the byte-order format may be glued to the length token (e.g. 16@1+).
            int atIndex = layout[1].IndexOf('@');
            if (atIndex >= 0)
            {
                if (!int.TryParse(layout[1].Substring(0, atIndex), out length)) return null;
                rawFormat = layout[1].Substring(atIndex + 1);
            }
            else
            {
                if (!int.TryParse(layout[1], out length)) return null;
            }
            if (length > 64) return null;

            CanByteOrder byteOrder = CanByteOrder.LittleEndian;
            bool isSigned = false;
            if (rawFormat.Length >= 2)
            {
                byteOrder = rawFormat[0] == '1' ? CanByteOrder.BigEndian : CanByteOrder.LittleEndian;
                isSigned = rawFormat[1] == '-';
            }

            double factor = 1.0, offset = 0.0;
            ParseScale(layoutTokens[1], ref factor, ref offset);

            double min = 0, max = 0;
            ParseRange(layoutTokens[2], ref min, ref max);

            string unit = string.Empty;
            string receivers = string.Empty;
            for (int i = 3; i < layoutTokens.Count; i++)
            {
                if (layoutTokens[i].StartsWith("\"", StringComparison.Ordinal))
                {
                    unit = StripQuotes(layoutTokens[i]);
                }
                else
                {
                    receivers += layoutTokens[i];
                }
            }

            return new DbcSignal
            {
                Name = name,
                MultiplexerIndicator = mux,
                StartBit = startBit,
                Length = length,
                ByteOrder = byteOrder,
                IsSigned = isSigned,
                Factor = factor,
                Offset = offset,
                Minimum = min,
                Maximum = max,
                Unit = unit,
            };
        }

        private static void ParseScale(string token, ref double factor, ref double offset)
        {
            // (factor,offset)
            string inner = token.Trim();
            if (inner.StartsWith("(", StringComparison.Ordinal)) inner = inner.Substring(1);
            if (inner.EndsWith(")", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1);
            var parts = inner.Split(',');
            if (parts.Length >= 2)
            {
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out factor);
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out offset);
            }
        }

        private static void ParseRange(string token, ref double min, ref double max)
        {
            string inner = token.Trim();
            if (inner.StartsWith("[", StringComparison.Ordinal)) inner = inner.Substring(1);
            if (inner.EndsWith("]", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1);
            var parts = inner.Split('|');
            if (parts.Length >= 2)
            {
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out min);
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out max);
            }
        }

        private static void ParseValueTable(string line, DbcDatabase database)
        {
            // VAL_ <msgId> <signalName> <value> "<label>" ...
            var tokens = SplitTopLevel(line.Substring(5).Trim());
            if (tokens.Count < 3) return;
            if (!uint.TryParse(tokens[0], out uint id)) return;
            string signalName = tokens[1];
            if (!database.TryGetMessage(id, out DbcMessage message)) return;
            if (!message.TryGetSignal(signalName, out DbcSignal signal)) return;

            signal.ValueDescriptions.Clear();
            for (int i = 2; i + 1 < tokens.Count; i += 2)
            {
                if (ulong.TryParse(tokens[i], NumberStyles.Any, CultureInfo.InvariantCulture, out ulong key))
                    signal.ValueDescriptions[key] = StripQuotes(tokens[i + 1]);
            }
        }

        private static void ParseComment(string line, DbcDatabase database)
        {
            // CM_ "comment" (compact) or CM_ SG_ <msgId> <signalName> "comment" / CM_ BO_ <msgId> "comment"
            var tokens = SplitTopLevel(line.Substring(4).Trim());
            if (tokens.Count < 2) return;

            if (tokens[0] == "BO_" && tokens.Count >= 3 &&
                uint.TryParse(tokens[1], out uint msgId) && database.TryGetMessage(msgId, out DbcMessage msg))
            {
                msg.Comment = StripQuotes(tokens[tokens.Count - 1]);
            }
            else if (tokens[0] == "SG_" && tokens.Count >= 4 &&
                uint.TryParse(tokens[1], out uint sgId) && database.TryGetMessage(sgId, out DbcMessage sgMsg))
            {
                if (sgMsg.TryGetSignal(tokens[2], out DbcSignal sgSignal))
                    sgSignal.Name = sgSignal.Name; // comment on signal ignored for now
            }
        }

        // ------------------------------------------------------------------
        // Tokenization helpers
        // ------------------------------------------------------------------

        private static List<string> SplitTopLevel(string text, char[] splitOn = null, bool trim = true)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                    continue;
                }
                if (!inQuotes && (splitOn == null ? char.IsWhiteSpace(c) : Array.IndexOf(splitOn, c) >= 0))
                {
                    if (current.Length > 0)
                    {
                        result.Add(trim ? current.ToString().Trim() : current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0)
                result.Add(trim ? current.ToString().Trim() : current.ToString());
            return result;
        }

        private static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Trim().TrimEnd(';').Trim().Trim('"').Trim();
        }
    }
}
