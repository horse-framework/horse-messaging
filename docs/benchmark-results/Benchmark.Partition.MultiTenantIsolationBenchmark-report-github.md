```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  Job-EUKMNY : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD

IterationCount=5  LaunchCount=1  WarmupCount=2  

```
| Method                       | TenantCount | MessageCount | Mean     | Error    | StdDev   | Rank | Gen0       | Gen1       | Allocated |
|----------------------------- |------------ |------------- |---------:|---------:|---------:|-----:|-----------:|-----------:|----------:|
| MultiTenant_With_NoisyTenant | 3           | 500          | 115.3 ms |  1.55 ms |  0.24 ms |    1 |  5187.5000 |  2625.0000 |  30.52 MB |
| MultiTenant_With_NoisyTenant | 8           | 500          | 187.7 ms |  2.48 ms |  0.65 ms |    2 |  8333.3333 |  4000.0000 |  49.16 MB |
| MultiTenant_With_NoisyTenant | 3           | 2000         | 466.6 ms | 87.40 ms | 22.70 ms |    3 | 21000.0000 | 11000.0000 | 123.43 MB |
| MultiTenant_With_NoisyTenant | 8           | 2000         | 751.1 ms | 15.69 ms |  4.07 ms |    4 | 34000.0000 | 17000.0000 | 204.32 MB |
