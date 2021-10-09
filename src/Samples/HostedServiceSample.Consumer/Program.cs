using HostedServiceSample.Client;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args);
service.Run();

internal class Program { }