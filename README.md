# SignalFlux — Engineering Signal & Data Computing for .NET

[![CI](https://github.com/teklot/SignalFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/teklot/SignalFlux/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/SignalFlux)](https://www.nuget.org/packages/SignalFlux)
[![.NET](https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue)](LICENSE)

Engineering data pipelines share an identical core: a `Signal` class, a `Measurement` struct, a way to carry units, a timestamp type, some quality enum. In practice these are re-implemented across separate repositories with divergent design choices, rarely composable and always coupled to a specific vendor or protocol. Without a common foundation, moving data between systems requires custom glue code at every seam.

SignalFlux is the domain model for engineering data on .NET, **the vocabulary that makes different systems speak the same language.** Not a math library, not a plotting engine, not a protocol. A shared type system that sits between your hardware and your analysis, giving every voltage reading, every temperature measurement, every experiment the same shape regardless of source.

**Guiding principle:** Never replace mature libraries. Standardize how they work together.

## Contents

- [The Problem](#the-problem)
- [Use Cases](#use-cases)
- [How It Works](#how-it-works)
  - [Units? No Magic Strings](#units-no-magic-strings)
  - [Immutability Without Boilerplate](#immutability-without-boilerplate)
  - [Streaming-First by Design](#streaming-first-by-design)
  - [Quality Is a First-Class Citizen](#quality-is-a-first-class-citizen)
  - [Composition, Not Inheritance](#composition-not-inheritance)
  - [Visualization Without Conversion Code](#visualization-without-conversion-code)
- [Technical Differentiators](#technical-differentiators)
- [Packages](#packages)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Domain Model](#domain-model)
  - [`Timestamp`](#timestamp)
  - [`Window`](#window)
  - [`Signal<T>`](#signalt)
  - [`Measurement<T>`](#measurementt)
  - [`Event` & `EventSeverity`](#event--eventseverity)
  - [`Experiment`](#experiment)
  - [`Session`](#session)
  - [`Result<T>`](#resultt)
  - [`Range<T>`](#ranget)
  - [`Metadata`](#metadata)
  - [`Quality`](#quality)
- [Supported Frameworks](#supported-frameworks)
- [Roadmap](#roadmap)

## The Problem

```csharp
// Typical codebase: every project reinvents
class MySignal { public double[] Data; public double SampleRate; }  // no units
class Timestamp { public long Ticks; }                              // no formatting
struct Measurement { public double Value; public string Unit; }     // magic strings
enum Quality { OK, Bad }                                            // underspecified
```

No two implementations agree. Units are `"V"` in one place, `"Volt"` in another, `null` in a third. Timestamps mix UTC, local, and unspecified. Quality is boolean, either "good" or nothing. Signals have no metadata, no source tracking, no way to trace where data came from. Pipelines between acquisition, storage, analysis, and visualization need bespoke adapters at every seam.

**SignalFlux eliminates the seam.** It provides the shared types that acquisition, processing, storage, and presentation all agree on, so your pipeline code moves data instead of converting it.

## Use Cases

Mixed protocols, mixed vendors, mixed storage, one shared `Signal`/`Measurement` vocabulary, no per-system converters:

- **Automotive / vehicle test benches:** stream engine RPM and coolant from a CAN network (PCAN, SocketCAN, or in-memory transport), decode signals from a DBC file, align them by timestamp, store to SQLite or Parquet, and replay against original timing.
- **Avionics bench test:** decode ARINC 429 words (parity + SSM verified) into altitude and airspeed `Measurement<T>` values, flagged `Quality.Bad` when the data source says so.
- **Industrial SCADA / test stands:** poll a PLC over Modbus (or an OPC UA server) and normalize mixed-vendor temperature, pressure, and current into one `Experiment` object for live plotting, reports, and storage.
- **Unmanned systems:** parse MAVLink attitude or NMEA GPS sentences over TCP/UDP/Serial into a single timeline merged with other instrument data.
- **Automated test & measurement:** generate stimulus (`SineGenerator`), capture response via `SignalFlux.IO`, compare to expected ranges, and archive the whole run as one `Experiment` (signals + events + config + equipment).
- **Simulation before hardware:** use `Generator` classes to produce realistic signals and exercise your acquisition-storage-analysis pipeline before real equipment arrives.
- **Regulatory & audit trails:** every `Measurement` carries a `Timestamp` and `Source`, every `Session` carries annotations, and `Metadata` attaches arbitrary key-value audit data, so the entire chain is preserved for review.

## How It Works

The entire domain model lives in `SignalFlux`, **built on UnitsNet for compile-time-safe units** with no other third-party runtime dependencies on either .NET 10 or .NET Standard 2.0.

```
┌──────────────────────────────────────────────────────────────┐
│                       SignalFlux                             │
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

Every type is **immutable by default**: `With*()` methods return new instances. Thread-safe by construction. No defensive copies needed.

### Units? No Magic Strings

Instead of passing `"V"`, `"Volt"`, or `null` through your pipeline:

```csharp
// SignalFlux uses UnitsNet enums: compile-time checked, IntelliSense discoverable
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

`Samples` is `ReadOnlyMemory<T>`; the backing array is shared across all derived copies. Zero allocations on the hot path.

### Streaming-First by Design

Signal generators expose both in-memory and streaming paths:

```csharp
// In-memory (for scripts, small data, testing):
Signal<double> signal = generator.GenerateSignal(1000);

// Streaming (for live acquisition, large datasets):
await foreach (var chunk in generator.GenerateStreaming(4096, totalChunks: 100))
    await writer.WriteSignalAsync(chunk);
```

Storage readers follow the same pattern: `CsvSignalReader` supports both `ReadAllSignalsAsync()` (in-memory) and `ReadStreamingAsync()` (chunked). Same for IO streams. The calling code chooses the tradeoff.

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

Every `Signal<T>` and `Measurement<T>` carries a `Quality`; no separate health channel needed.

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

`Experiment` groups related signals and events. `Session` groups experiments and replay metadata. Both are sealed classes with structural equality, not base types to extend.

### Visualization Without Conversion Code

SignalFlux types compose directly with plotting libraries: no manual `xs`/`ys` extraction, no time-axis conversion. A small adapter extension method bridges the gap:

```csharp
// Example: plotting a Signal<double> with ScottPlot 5
using SignalFlux;

public static ScottPlot.Plottables.Signal AddSignal<T>(
    this ScottPlot.Plot plot, Signal<T> signal, string label = null)
{
    var ys = signal.Samples.Span.ToArray().Select(x => Convert.ToDouble(x)).ToArray();
    var result = plot.Add.Signal(ys);
    result.Data.XOffset = signal.StartTime.DateTime.ToOADate();
    result.Data.Period = signal.SampleInterval.TotalDays;
    plot.Axes.DateTimeTicksBottom();
    if (signal.Unit != null) plot.YLabel(signal.Unit.ToString());
    return result;
}

// Usage: the signal becomes a native plottable on a real time axis
var plot = new ScottPlot.Plot();
plot.AddSignal(voltageSignal, "Voltage");
plot.AddSignal(currentSignal, "Current");
```

The pattern is the same for OxyPlot, LiveCharts, or any library that accepts x/y arrays: convert to `OADate` for time, use the UnitsNet unit for the axis label. The `Experiment` and `Event` types map to annotations and multi-series overlays the same way.

## Technical Differentiators

| vs. | SignalFlux |
|---|---|
| **Homemade Signal classes** | Zero-dependency core, immutable structs, `IEquatable<T>` everywhere, `UnitsNet`-typed units |
| **Math.NET** | Math.NET is algorithmic (FFT, linear algebra). SignalFlux is a domain model. They complement each other: `MathNet.Fourier.Forward(signal.Samples.Span)` |
| **OPC UA / MODBUS** | Protocol-specific. SignalFlux provides the protocol-independent types those adapters should produce |
| **Vendor SDKs** | Tied to hardware. SignalFlux normalizes data from any source into one shape |
| **Python (NumPy/Pandas)** | No static typing, no .NET interop. SignalFlux brings the same concept to .NET with `Memory<T>`, `Span<T>`, compile-time safety |

## Packages

| Package | Description |
|---|---|
| **SignalFlux** | Core domain model: `Signal<T>`, `Measurement<T>`, `Event`, `Experiment`, `Session`, `Result<T>`, `Metadata`, `Timestamp`, `Window`, `Range<T>`, `Quality` |
| **SignalFlux.TimeSeries** | Time-series operations: resampling, interpolation, alignment, windowing, statistics, downsampling |
| **SignalFlux.Generators** | Signal generators: sine, square, noise, ramp, sawtooth, random walk |
| **SignalFlux.IO** | Unified stream connection abstraction: TCP, UDP, Serial, Named Pipes with async, cancellation, timeouts |
| **SignalFlux.Storage** | CSV streaming read/write, SQLite & Parquet backends, `ISignalStore`/`IExperimentStore` interfaces, `SignalReplayer` |
| **SignalFlux.Protocols** | Protocol adapters for Modbus, MAVLink, NMEA 0183, CAN bus (DBC parser + decoder, Intel/Motorola signal encode-decode, in-memory transport), and ARINC 429 (32-bit word encode/decode with BNR + parity), bridging `Signal<T>` and `Measurement<T>` with real-world protocol data |
| **SignalFlux.OpcUa** | OPC UA client adapter: connect (anonymous / username+password), read, write, subscribe, browse; automatic reconnection with `OnStateChanged` events; engineering-unit resolution into typed `UnitsNet` units |

## Installation

```shell
dotnet add package SignalFlux
dotnet add package SignalFlux.TimeSeries
dotnet add package SignalFlux.Generators
dotnet add package SignalFlux.IO
dotnet add package SignalFlux.Storage
dotnet add package SignalFlux.Protocols
dotnet add package SignalFlux.OpcUa
```

> UnitsNet is automatically included as a dependency of SignalFlux. Add `using UnitsNet.Units;` to access typed unit enums like `ElectricPotentialUnit.Volt`, `TemperatureUnit.DegreeCelsius`, etc.

## Quick Start

```csharp
using SignalFlux;
using UnitsNet.Units;

// A precise moment in time
var now = Timestamp.UtcNow;

// A measurement: value, time, unit, quality
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

// A result type: success or failure
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
A uniformly sampled time-domain signal with frequency, unit, metadata, quality, and tags. All properties are immutable; use `With*()` for modifications.

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
A discriminated union representing success or failure, with no exceptions for control flow.

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

### Phase 1 — Foundation ✓
- SignalFlux, TimeSeries, Generators: all delivered

### Phase 2 — Data Acquisition ✓
- **SignalFlux.IO:** Unified `IStreamConnection` abstraction with TCP, UDP, Serial, Named Pipes adapters (async, cancellation, timeouts)
- **SignalFlux.Storage:** CSV streaming read/write, `ISignalStore`/`IExperimentStore` interfaces, SQLite (`SqliteSignalStore`, `SqliteExperimentStore`), Parquet (`ParquetSignalStore`) storage backends
- **SignalReplayer:** Replay signals from any `ISignalStore` with original timing support, integrated with `Session.CanReplay` flag
- **Samples:** Live acquisition pipeline demo (simulated sensor → TCP → Signal → CSV + SQLite)

### Phase 3 — Ecosystem ✓
- **SignalFlux.Protocols:** Protocol adapters for Modbus (`ModbusSignalExtensions`, `ModbusConnectionAdapter`), MAVLink v2 (`MavlinkSignalExtensions`, `MavlinkConnectionAdapter`), and NMEA 0183 (`NmeaSentenceExtensions`, `NmeaConnectionAdapter`), covering scale/offset/clamping, Signal/Measurement conversion, runtime dialect loading
- Later expanded (Phase 4) with CAN bus (DBC) and ARINC 429 support in the same package

### Phase 4 — Industry Integrations (in progress)
- **SignalFlux.OpcUa:** OPC UA client adapter ✓
  - Connect (anonymous or username/password), read, write, subscribe, browse
  - Automatic application certificate creation; untrusted-certificate acceptance policy (`OpcUaConnectionOptions`)
  - Automatic reconnection via keep-alive monitoring with `State` property and `OnStateChanged` events
  - Engineering-unit resolution: node `EUInformation` → typed `UnitsNet` unit on the measurement (`ReadNodeWithUnitAsync`, `OpcUaUnitMapper`)
- **SignalFlux.Protocols (CAN bus):** CAN frame model (`CanFrame`) and transport abstraction (`ICanTransport`) with in-memory loopback transport, plus Signal/Measurement encode-decode of CAN signals using Intel/Motorola bit layouts ✓
  - DBC file parser (`DbcParser`) and decoder (`DbcSignalDecoder`) with factor/offset scaling, multiplexing, value tables, and range-based quality
  - Native transport stubs: `SocketCanTransport` (Linux SocketCAN), `PcanTransport` (PCAN-Basic), `KvaserTransport` (CANlib) that throw clear platform/hardware-unavailable errors
- **SignalFlux.Protocols (ARINC 429):** 32-bit word encode/decode (`Arinc429Word`) with label/SDI/data/SSM/parity fields, odd/even parity helpers, and BNR data conversion to `Measurement<T>` ✓
- Later: device adapters, ML.NET / ONNX integration
