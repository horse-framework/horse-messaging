using AdvancedSample.Core.Service;

CoreService<Startup> service = new("stock-query-handler");
service.Registrar.AddTransientConsumers();
service.Start();

internal class Startup : IServiceStartup { };