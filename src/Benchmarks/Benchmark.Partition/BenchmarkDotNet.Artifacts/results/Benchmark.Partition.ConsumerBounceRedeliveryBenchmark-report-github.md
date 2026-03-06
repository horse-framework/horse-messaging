```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-ATPBMG : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                  | MessageCount | Mean     | Error       | StdDev      | Median    | Rank | Gen0      | Gen1      | Gen2     | Allocated   |
|------------------------ |------------- |---------:|------------:|------------:|----------:|-----:|----------:|----------:|---------:|------------:|
| Bounce_Buffer_Redeliver | 5000         | 166.0 ms |     9.00 ms |     1.39 ms | 165.97 ms |    1 | 6000.0000 | 1000.0000 | 333.3333 | 37612.07 KB |
| Bounce_Buffer_Redeliver | 100          | 567.6 ms | 1,978.41 ms |   513.79 ms | 942.78 ms |    2 |  125.0000 |   62.5000 |        - |   796.53 KB |
| Bounce_Buffer_Redeliver | 1000         | 894.3 ms | 4,523.43 ms | 1,174.72 ms |  38.10 ms |    2 | 1285.7143 |  500.0000 |  71.4286 |  7565.19 KB |
