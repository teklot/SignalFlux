# SignalFlux.Storage v0.1.0

- CsvSignalReader — read streaming CSV into Signal&lt;T&gt;
- CsvSignalWriter — write Signal&lt;T&gt; collections to CSV
- ISignalStore — extensible signal persistence interface
- IExperimentStore — extensible experiment persistence interface
- Reader supports both ReadAllSignalsAsync() and ReadStreamingAsync()
- Round-trip serialization with metadata preservation
- Depends on SignalFlux.Core
- Targets netstandard2.0 + net10.0
