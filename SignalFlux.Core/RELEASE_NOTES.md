# SignalFlux.Core v0.1.0

- Signal&lt;T&gt; — uniformly sampled time-domain signal with ReadOnlyMemory&lt;T&gt; samples, typed unit, quality, metadata, tags, source
- Measurement&lt;T&gt; — single timestamped data point with value, unit, quality
- Event / EventSeverity — structured event model (Debug, Info, Warning, Error, Critical)
- Experiment — groups signals + events + equipment + config + metadata
- Session — groups experiments with annotations and replay flag
- Result&lt;T&gt; — success/failure discriminated union
- Metadata — immutable-style IReadOnlyDictionary&lt;string, object&gt;
- Timestamp — UTC tick count with arithmetic and comparison operators
- Window — half-open interval [Start, End) with Contains / Overlaps
- Range&lt;T&gt; — closed interval for comparable types
- Quality — Unknown, Good, Fair, Poor, Bad, Invalid
- All types immutable with With*() pattern, IEquatable&lt;T&gt;
- Depends on UnitsNet 5.75 (typed unit enums)
- Targets netstandard2.0 + net10.0
