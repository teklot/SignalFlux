using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SignalFlux;
using SignalFlux.Generators;
using SignalFlux.TimeSeries;

namespace SignalFlux.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser]
    public class SignalBenchmarks
    {
        private Signal<double> _signal;

        [GlobalSetup]
        public void Setup()
        {
            var gen = new SineGenerator(frequency: 1000);
            _signal = gen.GenerateSignal(10000);
        }

        [Benchmark]
        public Signal<double> CreateSignal()
        {
            var samples = new double[1000];
            return new Signal<double>(samples, 1000, Timestamp.Now);
        }

        [Benchmark]
        public Signal<double> ResampleSignal()
        {
            return _signal.Resample(500);
        }

        [Benchmark]
        public SignalStatistics GetStatistics()
        {
            return _signal.Statistics();
        }
    }
}
