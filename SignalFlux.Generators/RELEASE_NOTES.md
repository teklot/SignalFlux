# SignalFlux.Generators v0.1.0

- SineGenerator — configurable frequency, amplitude, phase
- SquareGenerator — configurable duty cycle
- NoiseGenerator — white noise with configurable amplitude
- RampGenerator — linear ramp with slope
- SawtoothGenerator — sawtooth wave with period
- RandomWalkGenerator — Brownian motion with step size
- All generators support in-memory (GenerateSignal) and streaming (GenerateStreamAsync)
- All generators have WithUnit() for typed unit output
- Abstract SignalGenerator&lt;T&gt; base class for custom generators
- Depends on SignalFlux.Core
- Targets netstandard2.0 + net10.0
