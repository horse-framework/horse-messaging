```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-WFPSZR : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method               | ConsumerCount | MessageCount | Mean       | Error     | StdDev   | Rank | Gen0       | Gen1       | Allocated |
|--------------------- |-------------- |------------- |-----------:|----------:|---------:|-----:|-----------:|-----------:|----------:|
| OrphanLabelLess_Push | 1             | 5000         |   187.3 ms |   5.05 ms |  1.31 ms |    1 |  6000.0000 |  3000.0000 |  36.08 MB |
| OrphanLabelLess_Push | 3             | 5000         |   319.7 ms |   8.20 ms |  2.13 ms |    2 |  9333.3333 |  4666.6667 |     56 MB |
| OrphanLabelLess_Push | 8             | 5000         |   654.2 ms |   9.35 ms |  2.43 ms |    3 | 17000.0000 |  8500.0000 | 102.89 MB |
| OrphanLabelLess_Push | 1             | 20000        |   747.0 ms |  13.37 ms |  3.47 ms |    3 | 24000.0000 | 12000.0000 | 147.16 MB |
| OrphanLabelLess_Push | 3             | 20000        | 1,272.2 ms |  35.79 ms |  5.54 ms |    4 | 37000.0000 | 18000.0000 | 223.09 MB |
| OrphanLabelLess_Push | 8             | 20000        | 2,616.2 ms | 103.01 ms | 26.75 ms |    5 | 69000.0000 | 34000.0000 | 411.47 MB |
