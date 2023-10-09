using HorseService;

new ServiceBuilder()
    .SetOptions(AppOptions.LoadFromEnvironment())
    .ConfigureRider()
    .InitializeClusterOptions()
    .InitializeJockey()
    .CreateServer()
    .Run();