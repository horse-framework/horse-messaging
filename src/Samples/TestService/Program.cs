using AdvancedSample.Messaging.Common;
using AdvancedSample.Service;

ISampleService service = SampleServiceFactory.Create<Program>(AdvancedSampleServiceClientTypes.TestService, args);
service.Run();

internal class Program { }