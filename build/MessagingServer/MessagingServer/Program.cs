using MessagingServer;

await ServiceBuilder.Create()
    .SetOptions(OptionsBuilder
        .Create()
        .LoadFromEnvironment()
        .Build())
    .ConfigureRider()
    .InitializeClusterOptions()
    .InitializeJockey()
    .Build()
    .RunAsync(new CancellationTokenSource().Token);