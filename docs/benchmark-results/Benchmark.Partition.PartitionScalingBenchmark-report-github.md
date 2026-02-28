```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-GEGPDK : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                     | PartitionCount | Mean     | Error     | StdDev   | Rank | Gen0       | Gen1      | Gen2     | Allocated |
|--------------------------- |--------------- |---------:|----------:|---------:|-----:|-----------:|----------:|---------:|----------:|
| PartitionScale_LabeledPush | 5              | 200.5 ms |  57.10 ms | 14.83 ms |    1 | 13666.6667 | 6666.6667 |        - |  80.24 MB |
| PartitionScale_LabeledPush | 10             | 216.0 ms | 286.03 ms | 74.28 ms |    1 | 10000.0000 | 5000.0000 |        - |  58.89 MB |
| PartitionScale_LabeledPush | 20             | 220.2 ms |  48.72 ms | 12.65 ms |    1 | 11625.0000 | 5875.0000 | 187.5000 |  67.28 MB |
| PartitionScale_LabeledPush | 1              | 390.1 ms |  12.40 ms |  3.22 ms |    2 | 12500.0000 | 6500.0000 |        - |  76.05 MB |
