# SignalFlux — Engineering Computing for .NET

[![CI](https://github.com/teklot/SignalFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/teklot/SignalFlux/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/SignalFlux.Core)](https://www.nuget.org/packages/SignalFlux.Core)
[![.NET](https://img.shields.io/badge/.NET-net10.0%20%7C%20netstandard2.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue)](LICENSE)

Every engineering team I've worked with builds the same thing: a `Signal` class, a `Measurement` struct, a way to carry units, a timestamp type, some quality enum. Usually scattered across five repos, each with different design choices, none composable, all tied to a specific vendor or protocol. Data never flows between systems without custom glue code.

SignalFlux is the canonical domain model for engineering data on .NET — **the vocabulary that makes different systems speak the same language.** Not a math library, not a plotting engine, not a protocol. A shared type system that sits between your hardware and your analysis, giving every voltage reading, every temperature measurement, every experiment the same shape regardless of source.

**Guiding principle:** Never replace mature libraries. Standardize how they work together.

## The Problem

```csharp
// Typical codebase — every project reinvents:
class MySignal { public double[] Data; public double SampleRate; }  // no units
class Timestamp { public long Ticks; }                              // no formatting
struct Measurement { public double Value; public string Unit; }     // magic strings
enum Quality { OK, Bad }                                            // underspecified
```

No two implementations agree. Units are `"V"` in one place, `"Volt"` in another, `null` in a third. Timestamps mix UTC, local, and unspecified. Quality is boolean — either "good" or nothing. Signals have no metadata, no source tracking, no way to trace where data came from. Pipelines between acquisition, storage, analysis, and visualization need bespoke adapters at every seam.

**SignalFlux eliminates the seam.** It provides the shared types that acquisition, processing, storage, and presentation all agree on, so your pipeline code moves data instead of converting it.

## How It Works

The entire domain model lives in `SignalFlux.Core` — **zero third-party runtime dependencies** on either .NET 10 or .NET Standard 2.0.

```
┌──────────────────────────────────────────────────────────────┐
│                       SignalFlux.Core                        │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │   Signal<T>    │  │ Measurement<T> │  │     Event      │  │
│  │   .Samples     │  │   .Value       │  │   .Severity    │  │
│  │   .Frequency   │  │   .Timestamp   │  │   .Type        │  │
│  │   .Unit        │  │   .Unit        │  │   .Description │  │
│  │   .Quality     │  │   .Quality     │  │   .Source      │  │
│  │   .Tags        │  │   .Metadata    │  │                │  │
│  │   .Source      │  │                │  │                │  │
│  └────────────────┘  └────────────────┘  └────────────────┘  │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │   Timestamp    │  │    Window      │  │   Result<T>    │  │
│  │   .Ticks       │  │   .Start       │  │   .IsSuccess   │  │
│  │   .DateTime    │  │   .Duration    │  │   .Value       │  │
│  │   .CompareTo   │  │   .End         │  │   .Error       │  │
│  │   .ToUnixMs()  │  │   .Contains()  │  │   .GetValue…() │  │
│  │                │  │   .Overlaps()  │  │   .GetDef…()   │  │
│  │                │  │                │  │                │  │
│  └────────────────┘  └────────────────┘  └────────────────┘  │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │   Metadata     │  │    Range<T>    │  │    Quality     │  │
│  │   .With()      │  │   .Minimum     │  │   .Unknown     │  │
│  │   .ContainsKey │  │   .Maximum     │  │   .Good        │  │
│  │   .TryGetValue │  │   .Contains()  │  │   .Fair        │  │
│  │   .Keys        │  │                │  │   .Poor        │  │
│  │   .Values      │  │                │  │   .Bad         │  │
│  │   .Count       │  │                │  │   .Invalid     │  │
│  └────────────────┘  └────────────────┘  └────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│  │     Experiment (signals + events + config + equip)     │  │
│  │      Session (experiments + annotations + replay)      │  │ 
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

Every type is **immutable by default** — `With*()` methods return new instances. Thread-safe by construction. No defensive copies needed.

### Units? No Magic Strings

Instead of passing `"V"`, `"Volt"`, or `null` through your pipeline:

```csharp
// SignalFlux uses UnitsNet enums — compile-time checked, IntelliSense discoverable
new Signal<double>(data, 100, now, unit: ElectricPotentialUnit.Volt);
new Measurement<double>(24.5, now, unit: TemperatureUnit.DegreeCelsius);
```

The Unit property is typed `System.Enum`, accepting any of the ~100 unit enums UnitsNet defines. A `Volt` cannot accidentally be passed where `DegreeCelsius` is expected. No stringly-typed APIs.

### Immutability Without Boilerplate

Every value type exposes `With*()` methods for safe ad-hoc modification:

```csharp
var raw = new Signal<double>(samples, 100, now, unit: ElectricPotentialUnit.Volt);
// Re-express in different units without copying samples:
var mv   = raw.WithUnit(ElectricPotentialUnit.Millivolt);
// Adjust timing without reallocating the array:
var late = raw.WithStartTime(raw.StartTime + TimeSpan.FromSeconds(5));
```

`Samples` is `ReadOnlyMemory<T>` — the backing array is shared across all derived copies. Zero allocations on the hot path.

### Streaming-First by Design

Signal generators expose both in-memory and streaming paths:

```csharp
// In-memory — for scripts, small data, testing:
Signal<double> signal = generator.GenerateSignal(1000);

// Streaming — for live acquisition, large datasets:
await foreach (var chunk in generator.GenerateStreaming(4096, totalChunks: 100))
    await writer.WriteSignalAsync(chunk);
```

Storage readers follow the same pattern — `CsvSignalReader` supports both `ReadAllSignalsAsync()` (in-memory) and `ReadStreamingAsync()` (chunked). Same for IO streams. The calling code chooses the tradeoff.

### Quality Is a First-Class Citizen

Data degrades. Sensors saturate. Networks drop packets. Quality is not a boolean:

| Value | Meaning |
|---|---|
| `Unknown` | No quality assessment available |
| `Good` | Full confidence in the data |
| `Fair` | Usable but degraded (e.g., high noise) |
| `Poor` | Questionable data, use with caution |
| `Bad` | Known bad, but preserved for audit |
| `Invalid` | Not valid under any interpretation |

Every `Signal<T>` and `Measurement<T>` carries a `Quality` — no separate health channel needed.

### Composition, Not Inheritance

```csharp
// Assemble building blocks, don't extend base classes:
var exp = new Experiment(
    id: "EXP-001",
    signals: new Dictionary<string, object> { { "voltage", vSignal }, { "current", iSignal } },
    events: new[] { alarm },
    start: Timestamp.UtcNow,
    equipment: new[] { "DAQ-01" });

var session = new Session("SES-001", experiments: new[] { exp }, canReplay: true)
    .WithAnnotation("Pre-flight complete");
```

`Experiment` groups related signals and events. `Session` groups experiments and replay metadata. Both are sealed classes with structural equality — not base types to extend.

## Use Cases

### Data Acquisition Pipeline

```
Sensor → SignalFlux.Measurement → SignalFlux.Signal → SignalFlux.Experiment → SignalFlux.Storage
```

Every sample gets a timestamp, a unit, a quality assessment, and optional metadata before it ever touches disk. Downstream code reads the same types — no parse phase, no schema negotiation.

### Multi-Vendor Telemetry

Two different DAQ systems produce `Signal<double>` with different units, rates, and start times. SignalFlux.TimeSeries provides alignment, resampling, and merging — across vendors, across protocols, across formats. The domain model is the shared contract.

### Automated Test & Measurement

```
Test Script → SignalFlux.Testing.SineGenerator → Capture → Compare → PASS/FAIL
```

Generate stimulus signals, capture response via SignalFlux.IO, compare against expected ranges, archive the entire experiment (signals + events + config + equipment list) as a single `Experiment` object.

### Regulatory & Audit Trails

Every `Measurement` has a `Timestamp` and `Source`. Every `Experiment` captures `Operator`, `Equipment`, and `Configuration`. Every `Session` carries annotations. `Metadata` attaches arbitrary key-value audit data to any domain object. The entire chain is preserved for review.

## Technical Differentiators

| vs. | SignalFlux |
|---|---|
| **Homemade Signal classes** | Zero-dependency core, immutable structs, `IEquatable<T>` everywhere, `UnitsNet`-typed units |
| **Math.NET** | Math.NET is algorithmic (FFT, linear algebra). SignalFlux is a domain model. They complement each other: `MathNet.Fourier.Forward(signal.Samples.Span)` |
| **OPC UA / MODBUS** | Protocol-specific. SignalFlux provides the protocol-independent types those adapters should produce |
| **Vendor SDKs** | Tied to hardware. SignalFlux normalizes data from any source into one shape |
| **Python (NumPy/Pandas)** | No static typing, no .NET interop. SignalFlux brings the same concept to .NET with `Memory<T>`, `Span<T>`, compile-time safety |

## Packages

| Package | Description | Status |
|---|---|---|
| **SignalFlux.Core** | Core domain model: `Signal<T>`, `Measurement<T>`, `Event`, `Experiment`, `Session`, `Result<T>`, `Metadata`, `Timestamp`, `Window`, `Range<T>`, `Quality` | Phase 1 |
| **SignalFlux.TimeSeries** | Time-series operations: resampling, interpolation, alignment, windowing, statistics, downsampling | Phase 1 |
| **SignalFlux.Testing** | Signal generators: sine, square, noise, ramp, sawtooth, random walk | Phase 1 |
| **SignalFlux.IO** | Unified stream connection abstraction: TCP, UDP, Serial, Named Pipes with async, cancellation, timeouts | Phase 2 |
| **SignalFlux.Storage** | Storage adapters: CSV streaming read/write, `ISignalStore`/`IExperimentStore` interfaces | Phase 2 |

## Installation

```shell
dotnet add package SignalFlux.Core
dotnet add package SignalFlux.TimeSeries
dotnet add package SignalFlux.Testing
```

> UnitsNet is automatically included as a dependency of SignalFlux.Core. Add `using UnitsNet.Units;` to access typed unit enums like `ElectricPotentialUnit.Volt`, `TemperatureUnit.DegreeCelsius`, etc.

## Quick Start

```csharp
using SignalFlux;
using UnitsNet.Units;

// A precise moment in time
var now = Timestamp.UtcNow;

// A measurement — value, time, unit, quality
var meas = new Measurement<double>(
    value: 24.5,
    timestamp: now,
    unit: TemperatureUnit.DegreeCelsius,
    quality: Quality.Good);

// A time window
var window = new Window(now, TimeSpan.FromSeconds(10));

// A uniformly sampled signal
var samples = new double[] { 1.0, 1.5, 2.0, 2.5, 3.0 };
var signal = new Signal<double>(
    samples: samples.AsMemory(),
    frequency: 100,           // 100 Hz
    startTime: now,
    unit: ElectricPotentialUnit.Volt);

// Immutable copies via With*() pattern
var adjusted = signal.WithUnit(ElectricPotentialUnit.Millivolt).WithFrequency(200);

// Annotated metadata
var meta = new Metadata()
    .With("sensor", "PT-100")
    .With("location", "Reactor A");
var tagged = signal.WithMetadata(meta);

// An event during an experiment
var alarm = new Event(
    time: now,
    severity: EventSeverity.Warning,
    type: "OverTemp",
    description: "Temperature exceeded threshold",
    source: "Sensor-01");

// A result type — success or failure
var ok = Result<int>.Ok(42);
var fail = Result<int>.Fail("Sensor not responding");

// Group signals and events into an experiment
var experiment = new Experiment(
    id: "EXP-001",
    signals: new Dictionary<string, object> { { "voltage", signal } },
    events: new[] { alarm },
    start: now,
    tags: new Dictionary<string, string> { { "project", "qualification" } });

// Group experiments into a session
var session = new Session(
    id: "SES-001",
    experiments: new[] { experiment },
    canReplay: true);
```

## Domain Model

### `Timestamp`
A precise moment in time as a UTC tick count. Supports arithmetic, comparison, Unix conversion, and ISO 8601 formatting.

```csharp
var t1 = Timestamp.UtcNow;
var t2 = Timestamp.FromUnixMilliseconds(1700000000000);
var elapsed = t1 - t2;           // TimeSpan
var later = t1 + TimeSpan.FromHours(1);
bool ordered = t1 < t2;          // comparison operators
```

### `Window`
A half-open time interval `[Start, End)` with a positive duration.

```csharp
var w = new Window(start, TimeSpan.FromSeconds(5));
bool inside = w.Contains(timestamp);
bool overlap = w.Overlaps(other);
```

### `Signal<T>`
A uniformly sampled time-domain signal with frequency, unit, metadata, quality, and tags. All properties are immutable — use `With*()` for modifications.

```csharp
var s = new Signal<double>(samples, frequency: 100, startTime: now, unit: ElectricPotentialUnit.Volt);
int count = s.Count;
TimeSpan dur = s.Duration;
TimeSpan dt = s.SampleInterval;
Timestamp end = s.EndTime;
var copy = s.WithSamples(newSamples).WithFrequency(200).WithUnit(ElectricPotentialUnit.Millivolt);
```

### `Measurement<T>`
A single timestamped data point with value, unit, quality, and optional metadata.

```csharp
var m = new Measurement<double>(98.6, Timestamp.UtcNow, unit: TemperatureUnit.DegreeFahrenheit);
var c = m.WithValue(37.0).WithUnit(TemperatureUnit.DegreeCelsius);
```

### `Event` & `EventSeverity`
A notable occurrence at a specific time with severity (`Debug`, `Info`, `Warning`, `Error`, `Critical`), a machine-readable type, human-readable description, and optional source.

```csharp
var e = new Event(
    time: Timestamp.UtcNow,
    severity: EventSeverity.Error,
    type: "CommsLost",
    description: "Connection to sensor timed out",
    source: "Gateway-01");
```

### `Experiment`
Groups signals (keyed by name), events, configuration, equipment, and tags into a single experimental run.

```csharp
var exp = new Experiment(
    id: "EXP-001",
    signals: dict,
    events: events,
    start: Timestamp.UtcNow,
    end: Timestamp.UtcNow + TimeSpan.FromMinutes(5),
    equipment: new[] { "DAQ-01", "Thermocouple-Bank" });
```

### `Session`
Groups multiple experiments with annotations and a replay flag.

```csharp
var ses = new Session("SES-001", experiments, canReplay: true)
    .WithAnnotation("Pre-flight check complete");
```

### `Result<T>`
A discriminated union representing success or failure — no exceptions for control flow.

```csharp
var r = Result<double>.Ok(3.14);
double val = r.GetValueOrThrow();          // throws if failed
double fallback = r.GetValueOrDefault(0);  // safe default
```

### `Range<T>`
A closed interval `[Minimum, Maximum]` for comparable types.

```csharp
var r = new Range<double>(0.0, 100.0);
bool ok = r.Contains(42.0);  // true
```

### `Metadata`
An immutable-style `IReadOnlyDictionary<string, object>` key-value store. The `With()` method returns a new instance with the added entry.

```csharp
var m = new Metadata()
    .With("sensor", "PT-100")
    .With("calibration_date", "2026-01-15");
object value = m["sensor"];
```

### `Quality`
An enum describing data confidence: `Unknown`, `Good`, `Fair`, `Poor`, `Bad`, `Invalid`.

## Supported Frameworks

- **.NET 10+**: Optimized for maximum performance and Native AOT compatibility.
- **.NET Standard 2.0**: Broad compatibility across legacy .NET platforms.

## Roadmap

- **Phase 1** (Foundation): Core domain, TimeSeries, Testing ✓
- **Phase 2** (Data Acquisition): IO, Storage ✓
- **Phase 3** (Ecosystem): Protocols, Visualization
- **Phase 4** (Industry Integrations): OPC UA, CAN, devices, ML
