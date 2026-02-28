using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace Benchmark.Partition;

/// <summary>
/// Entry point for the Partition Queue System benchmark suite.
///
/// Run with:
///   dotnet run -c Release -- [filter]
///
/// Examples:
///   dotnet run -c Release                              # run ALL benchmarks
///   dotnet run -c Release -- --filter *Labeled*        # only labeled-push benchmarks
///   dotnet run -c Release -- --filter *Routing*        # routing cost only
///   dotnet run -c Release -- --filter *VsFlat*         # partition vs RoundRobin comparison
///   dotnet run -c Release -- --list flat               # list all benchmark method names
///
/// Output
///   BenchmarkDotNet writes results to ./BenchmarkDotNet.Artifacts/
///   CSV and Markdown reports are generated automatically.
///   After a run, copy the *.md file into docs/ and update the two partition summary docs.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        // ── Config ────────────────────────────────────────────────────────────
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(
                Job.Default
                   .WithLaunchCount(1)   // single process launch per benchmark
                   .WithWarmupCount(2)   // 2 warmup iterations
                   .WithIterationCount(5) // 5 measured iterations — keeps runtime sane
                                          // for integration-style benchmarks
            )
            .AddExporter(MarkdownExporter.GitHub)   // → BenchmarkDotNet.Artifacts/*.md
            .AddExporter(CsvExporter.Default)       // → BenchmarkDotNet.Artifacts/*.csv
            .AddLogger(ConsoleLogger.Default)
            .AddValidator(ExecutionValidator.FailOnError)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator); // allow Debug builds for quick checks

        // ── Runner ────────────────────────────────────────────────────────────
        // BenchmarkSwitcher respects --filter arguments from the command line.
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args, config);
    }
}

