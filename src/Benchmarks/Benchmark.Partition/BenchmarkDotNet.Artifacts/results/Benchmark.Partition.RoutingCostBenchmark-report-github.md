```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-ZVXACJ : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                   | PartitionCount | Mean        | Error      | StdDev    | Rank | Gen0     | Gen1    | Allocated |
|------------------------- |--------------- |------------:|-----------:|----------:|-----:|---------:|--------:|----------:|
| Routing_LabelLookup_Push | 1              |    38.54 μs |   1.802 μs |  0.468 μs |    1 |   1.2817 |  0.6714 |   7.78 KB |
| Routing_LabelLookup_Push | 10             |   379.07 μs |   8.709 μs |  2.262 μs |    2 |  13.1836 |  6.8359 |  79.57 KB |
| Routing_LabelLookup_Push | 50             | 2,068.36 μs | 107.490 μs | 27.915 μs |    3 |  68.3594 | 33.2031 | 414.42 KB |
| Routing_LabelLookup_Push | 100            | 4,439.13 μs | 334.974 μs | 86.992 μs |    4 | 140.6250 | 70.3125 | 866.64 KB |
