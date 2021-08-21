using AdvancedSample.Messaging.Common;
using AdvancedSample.Service;

SampleService<Program> service = new(AdvancedSampleServiceClientTypes.TestService, args);
service.Run();

internal class Program { }

