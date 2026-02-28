```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-EXAVUY : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                      | FanOut | Mean      | Error    | StdDev   | Rank | Gen0      | Gen1      | Gen2    | Allocated |
|---------------------------- |------- |----------:|---------:|---------:|-----:|----------:|----------:|--------:|----------:|
| Broadcast_Push_PerPartition | 1      |  82.38 ms | 2.506 ms | 0.388 ms |    1 | 2687.5000 | 1375.0000 |       - |  15.95 MB |
| Broadcast_Push_PerPartition | 2      | 110.28 ms | 1.440 ms | 0.223 ms |    2 | 3375.0000 | 1687.5000 |       - |   19.9 MB |
| Broadcast_Push_PerPartition | 5      | 194.87 ms | 5.300 ms | 1.376 ms |    3 | 5250.0000 | 2625.0000 | 62.5000 |  31.19 MB |
