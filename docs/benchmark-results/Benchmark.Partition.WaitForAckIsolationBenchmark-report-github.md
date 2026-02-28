```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-ICVGWO : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                 | WorkerCount | MessageCount | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|----------------------- |------------ |------------- |----------:|----------:|----------:|------:|--------:|-----:|---------:|---------:|-----------:|------------:|
| WaitForAck_FlatQueue   | 2           | 200          |  4.618 ms | 0.1097 ms | 0.0285 ms |  1.00 |    0.01 |    1 | 140.6250 |  23.4375 |  858.15 KB |        1.00 |
| WaitForAck_Partitioned | 2           | 200          |  5.214 ms | 0.2141 ms | 0.0556 ms |  1.13 |    0.01 |    1 | 164.0625 |  31.2500 | 1014.85 KB |        1.18 |
|                        |             |              |           |           |           |       |         |      |          |          |            |             |
| WaitForAck_FlatQueue   | 2           | 1000         | 21.788 ms | 1.2866 ms | 0.3341 ms |  1.00 |    0.02 |    1 | 687.5000 | 125.0000 | 4295.81 KB |        1.00 |
| WaitForAck_Partitioned | 2           | 1000         | 26.341 ms | 0.5204 ms | 0.1351 ms |  1.21 |    0.02 |    1 | 875.0000 | 125.0000 | 5320.05 KB |        1.24 |
|                        |             |              |           |           |           |       |         |      |          |          |            |             |
| WaitForAck_Partitioned | 4           | 200          |  4.158 ms | 0.2697 ms | 0.0417 ms |  0.94 |    0.02 |    1 | 164.0625 |  31.2500 |    1020 KB |        1.18 |
| WaitForAck_FlatQueue   | 4           | 200          |  4.429 ms | 0.3711 ms | 0.0964 ms |  1.00 |    0.03 |    1 | 140.6250 |  31.2500 |  862.63 KB |        1.00 |
|                        |             |              |           |           |           |       |         |      |          |          |            |             |
| WaitForAck_FlatQueue   | 4           | 1000         | 21.969 ms | 1.6913 ms | 0.4392 ms |  1.00 |    0.03 |    1 | 812.5000 | 156.2500 | 5046.33 KB |        1.00 |
| WaitForAck_Partitioned | 4           | 1000         | 22.911 ms | 6.8495 ms | 1.0600 ms |  1.04 |    0.05 |    1 | 812.5000 | 125.0000 | 5014.15 KB |        0.99 |
