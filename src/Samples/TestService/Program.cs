using AdvancedSample.Service;

SampleService<Program> service = new SampleService<Program>("testClient", args);
service.Run();

internal class Program {}