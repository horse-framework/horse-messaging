using System;
using System.Diagnostics;

namespace Test.Server.Benchmarks;

/// <summary>
/// Captures CPU time, RAM and elapsed time metrics for benchmark assertions.
/// </summary>
public class BenchmarkMetrics
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _baselineCpuTime;
    private long _baselineRam;

    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public TimeSpan CpuDelta => Process.GetCurrentProcess().TotalProcessorTime - _baselineCpuTime;
    public long RamDeltaBytes => Process.GetCurrentProcess().WorkingSet64 - _baselineRam;

    /// <summary>
    /// Records baseline CPU and RAM, starts the stopwatch.
    /// </summary>
    public void Start()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        _baselineCpuTime = proc.TotalProcessorTime;
        _baselineRam = proc.WorkingSet64;
        _stopwatch.Restart();
    }

    /// <summary>
    /// Stops the stopwatch and returns a snapshot.
    /// </summary>
    public BenchmarkSnapshot Stop()
    {
        _stopwatch.Stop();

        var proc = Process.GetCurrentProcess();
        return new BenchmarkSnapshot
        {
            Elapsed = _stopwatch.Elapsed,
            CpuTimeDelta = proc.TotalProcessorTime - _baselineCpuTime,
            RamDeltaBytes = proc.WorkingSet64 - _baselineRam
        };
    }

    /// <summary>
    /// Calculates throughput as messages per second.
    /// </summary>
    public static double Throughput(int messageCount, TimeSpan elapsed)
    {
        return elapsed.TotalSeconds > 0 ? messageCount / elapsed.TotalSeconds : 0;
    }
}

public class BenchmarkSnapshot
{
    public TimeSpan Elapsed { get; init; }
    public TimeSpan CpuTimeDelta { get; init; }
    public long RamDeltaBytes { get; init; }

    public double RamDeltaMB => RamDeltaBytes / (1024.0 * 1024.0);

    public void PrintSummary(string phase, int messageCount, ITestOutputSink output)
    {
        double throughput = BenchmarkMetrics.Throughput(messageCount, Elapsed);
        output.WriteLine($"[{phase}] Elapsed: {Elapsed.TotalMilliseconds:F0} ms");
        output.WriteLine($"[{phase}] CPU Time Delta: {CpuTimeDelta.TotalMilliseconds:F0} ms");
        output.WriteLine($"[{phase}] RAM Delta: {RamDeltaMB:F2} MB");
        output.WriteLine($"[{phase}] Throughput: {throughput:F0} msg/sec");
    }
}

/// <summary>
/// Abstraction for test output so BenchmarkSnapshot can write diagnostics.
/// </summary>
public interface ITestOutputSink
{
    void WriteLine(string message);
}

