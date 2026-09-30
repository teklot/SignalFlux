using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalFlux.IO;

namespace SignalFlux.Protocols.Scpi
{
    /// <summary>
    /// A vendor-neutral SCPI (Standard Commands for Programmable Instruments) client. Sends commands and
    /// queries over an <see cref="IStreamConnection"/>, parses line-oriented responses, and reads
    /// IEEE-488.2 binary blocks. Written for the common subset shared by most instruments
    /// (*IDN?, common commands, and block data); full instrument-specific command sets are out of scope.
    /// </summary>
    public sealed class ScpiClient
    {
        private readonly byte[] _terminatorBytes;

        /// <summary>Creates a SCPI client over the given connection using the default newline (LF) line terminator.</summary>
        /// <param name="connection">The duplex stream connection to the instrument.</param>
        public ScpiClient(IStreamConnection connection)
            : this(connection, "\n")
        {
        }

        /// <summary>Creates a SCPI client over the given connection.</summary>
        /// <param name="connection">The duplex stream connection to the instrument.</param>
        /// <param name="lineTerminator">The line terminator used between commands and responses (e.g., "\n" or "\r\n").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="lineTerminator"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="lineTerminator"/> is empty.</exception>
        public ScpiClient(IStreamConnection connection, string lineTerminator)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            if (lineTerminator == null) throw new ArgumentNullException(nameof(lineTerminator));
            if (lineTerminator.Length == 0) throw new ArgumentException("Line terminator must not be empty.", nameof(lineTerminator));
            _terminatorBytes = Encoding.ASCII.GetBytes(lineTerminator);
            LineTerminator = lineTerminator;
        }

        /// <summary>The underlying stream connection to the instrument.</summary>
        public IStreamConnection Connection { get; }

        /// <summary>The line terminator appended to outgoing commands and expected on incoming responses.</summary>
        public string LineTerminator { get; }

        /// <summary>Ensures the underlying connection is established, connecting when necessary.</summary>
        /// <param name="cancellationToken">Token to cancel the connect operation.</param>
        /// <returns>A task that completes when the connection is ready.</returns>
        public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (Connection.State != ConnectionState.Connected)
                await Connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Sends a command to the instrument without waiting for a response.</summary>
        /// <param name="command">The SCPI command (terminator is appended automatically).</param>
        /// <param name="cancellationToken">Token to cancel the write operation.</param>
        /// <returns>A task that completes once the command has been written.</returns>
        public Task WriteAsync(string command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return WriteCoreAsync(command, cancellationToken);
        }

        private async Task WriteCoreAsync(string command, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            byte[] payload = new byte[Encoding.ASCII.GetByteCount(command) + _terminatorBytes.Length];
            int written = Encoding.ASCII.GetBytes(command, 0, command.Length, payload, 0);
            Array.Copy(_terminatorBytes, 0, payload, written, _terminatorBytes.Length);
            await Connection.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Sends a query and reads the line-oriented response (terminated by the configured line terminator).</summary>
        /// <param name="query">The SCPI query ending with '?'.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The response line without the line terminator.</returns>
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return QueryCoreAsync(query, cancellationToken);
        }

        private async Task<string> QueryCoreAsync(string query, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            await WriteCoreAsync(query, cancellationToken).ConfigureAwait(false);
            return await ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Sends the *IDN? identification query and returns the instrument identification string.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>An identification string of the form "MANUFACTURER,MODEL,SERIAL,VERSION".</returns>
        public Task<string> IdnAsync(CancellationToken cancellationToken = default)
            => QueryAsync("*IDN?", cancellationToken);

        /// <summary>Reads one line from the connection up to the configured line terminator.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The line without the terminator and without a trailing carriage return.</returns>
        public async Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            var builder = new StringBuilder();
            while (true)
            {
                int value = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                if (value == '\n')
                    break;
                if (value != '\r')
                    builder.Append((char)value);
            }
            return builder.ToString();
        }

        /// <summary>Sends a query and reads the response as an IEEE-488.2 binary block.</summary>
        /// <param name="query">The SCPI query ending with '?'.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The parsed block payload.</returns>
        public Task<ScpiBlock> QueryBlockAsync(string query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return QueryBlockCoreAsync(query, cancellationToken);
        }

        private async Task<ScpiBlock> QueryBlockCoreAsync(string query, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            await WriteCoreAsync(query, cancellationToken).ConfigureAwait(false);
            return await ReadBlockAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads an IEEE-488.2 binary block (definite- or indefinite-length) from the connection.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The parsed block.</returns>
        /// <exception cref="ScpiProtocolException">Thrown when the response is not a valid IEEE-488.2 block.</exception>
        public async Task<ScpiBlock> ReadBlockAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            int hash = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (hash != '#')
                throw new ScpiProtocolException("Expected IEEE-488.2 block header starting with '#'.");
            int header = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (header < '0' || header > '9')
                throw new ScpiProtocolException("Expected a length-code digit after '#' in IEEE-488.2 block header.");
            int digitCount = header - '0';
            if (digitCount == 0)
            {
                var indefinite = new List<byte>();
                while (true)
                {
                    int value = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                    if (value == '\n')
                        break;
                    if (value != '\r')
                        indefinite.Add((byte)value);
                }
                return new ScpiBlock(indefinite.ToArray());
            }
            long length = 0;
            for (int i = 0; i < digitCount; i++)
            {
                int digit = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                if (digit < '0' || digit > '9')
                    throw new ScpiProtocolException("Invalid digit in IEEE-488.2 block length field.");
                length = length * 10 + (digit - '0');
            }
            var data = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                if (Connection.State != ConnectionState.Connected)
                    throw new ScpiProtocolException("Connection is not connected while reading a SCPI block.");
                int read = await Connection.ReadAsync(data.AsMemory(offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    await Task.Yield();
                    continue;
                }
                offset += read;
            }
            return new ScpiBlock(data);
        }

        private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
        {
            var one = new byte[1];
            while (true)
            {
                if (Connection.State != ConnectionState.Connected)
                    throw new ScpiProtocolException("Connection is not connected while reading a SCPI response.");
                int read = await Connection.ReadAsync(one, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                    return one[0];
                await Task.Yield();
            }
        }
    }
}