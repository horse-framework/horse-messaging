using MessagingServer;

ServiceBuilder.Create()
    .SetOptions(OptionsBuilder
        .Create()
        .LoadFromEnvironment()
        .Build())
    .ConfigureRider()
    .InitializeClusterOptions()
    .InitializeJockey()
    .Build()
    .Run();