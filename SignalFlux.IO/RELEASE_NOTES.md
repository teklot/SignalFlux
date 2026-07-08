# SignalFlux.IO v0.1.0

- IStreamConnection — unified async stream abstraction
- TcpConnection — TCP client with connect/read/write/timeout
- UdpConnection — UDP client with send/receive/timeout
- SerialConnection — serial port with baud rate, parity, stop bits
- NamedPipeConnection — named pipe client/server
- ConnectionState — Connected, Disconnected, Reconnecting, Faulted
- ConnectionOptions — configurable timeouts, buffer sizes, reconnect policy
- All connections support CancellationToken
- Depends on SignalFlux.Core
- Targets netstandard2.0 + net10.0
