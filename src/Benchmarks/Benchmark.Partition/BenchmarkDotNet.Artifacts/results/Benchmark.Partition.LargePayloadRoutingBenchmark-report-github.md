```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-HTNLQP : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

InvocationCount=1  IterationCount=5  LaunchCount=1  
UnrollFactor=1  WarmupCount=2  

```
| Method                    | PayloadKB | PartitionCount | Mean       | Error      | StdDev     | Rank | Gen0       | Gen1       | Gen2       | Allocated |
|-------------------------- |---------- |--------------- |-----------:|-----------:|-----------:|-----:|-----------:|-----------:|-----------:|----------:|
| LargePayload_Labeled_Push | 16        | 10             |   4.567 ms |  0.3592 ms |  0.0933 ms |    1 |  3000.0000 |  1000.0000 |          - |  16.26 MB |
| LargePayload_Labeled_Push | 64        | 10             |  13.823 ms |  1.3429 ms |  0.2078 ms |    2 | 11000.0000 |  4000.0000 |          - |  63.14 MB |
| LargePayload_Labeled_Push | 256       | 10             |  19.051 ms | 27.8866 ms |  4.3155 ms |    3 |  3000.0000 |  3000.0000 |  3000.0000 |  250.7 MB |
| LargePayload_Labeled_Push | 1         | 10             |  27.944 ms | 27.2128 ms |  7.0671 ms |    4 |  1000.0000 |          - |          - |   7.34 MB |
| LargePayload_Labeled_Push | 1         | 1              |  28.342 ms |  5.1019 ms |  0.7895 ms |    4 |  1000.0000 |  1000.0000 |          - |   7.29 MB |
| LargePayload_Labeled_Push | 16        | 1              |  46.531 ms |  1.0369 ms |  0.1605 ms |    5 | 12000.0000 |  6000.0000 |          - |   66.4 MB |
| LargePayload_Labeled_Push | 64        | 1              | 123.442 ms | 87.9029 ms | 22.8281 ms |    6 | 43000.0000 | 21000.0000 |  6000.0000 | 251.62 MB |
| LargePayload_Labeled_Push | 256       | 1              | 194.806 ms | 64.9783 ms | 16.8747 ms |    7 | 61000.0000 | 61000.0000 | 61000.0000 | 976.11 MB |
