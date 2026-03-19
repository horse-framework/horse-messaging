```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-FUUYLY : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                        | PartitionsToCreate | Mean      | Error      | StdDev    | Rank | Gen0      | Gen1      | Gen2      | Allocated   |
|------------------------------ |------------------- |----------:|-----------:|----------:|-----:|----------:|----------:|----------:|------------:|
| Partition_Create_Push_Destroy | 1                  |  23.40 ms |   7.932 ms |  2.060 ms |    1 |   78.1250 |   62.5000 |   62.5000 |   881.62 KB |
| Partition_Create_Push_Destroy | 5                  | 102.31 ms |  79.272 ms | 20.587 ms |    2 |  750.0000 |  750.0000 |  625.0000 | 12020.02 KB |
| Partition_Create_Push_Destroy | 10                 | 217.44 ms | 213.341 ms | 55.404 ms |    3 | 1200.0000 | 1000.0000 | 1000.0000 | 24945.97 KB |
