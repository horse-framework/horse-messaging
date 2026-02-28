```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-ZVXACJ : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                 | PartitionCount | MessageCount | Mean      | Error     | StdDev    | Rank | Gen0       | Gen1      | Allocated |
|----------------------- |--------------- |------------- |----------:|----------:|----------:|-----:|-----------:|----------:|----------:|
| LabeledPush_Throughput | 4              | 1000         |  37.98 ms |  2.489 ms |  0.646 ms |    1 |  1312.5000 |  687.5000 |   7.97 MB |
| LabeledPush_Throughput | 10             | 1000         |  38.20 ms |  2.037 ms |  0.529 ms |    1 |  1312.5000 |  687.5000 |   7.92 MB |
| LabeledPush_Throughput | 1              | 1000         |  38.76 ms |  1.527 ms |  0.236 ms |    1 |  1250.0000 |  625.0000 |   7.63 MB |
| LabeledPush_Throughput | 4              | 10000        | 376.21 ms |  3.629 ms |  0.562 ms |    2 | 13500.0000 | 7000.0000 |  79.71 MB |
| LabeledPush_Throughput | 1              | 10000        | 385.14 ms |  8.170 ms |  2.122 ms |    2 | 12500.0000 | 6500.0000 |  76.38 MB |
| LabeledPush_Throughput | 10             | 10000        | 388.82 ms | 59.976 ms | 15.576 ms |    2 | 13000.0000 | 7000.0000 |  80.18 MB |
