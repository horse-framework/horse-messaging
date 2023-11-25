namespace AdvancedSample.Service;

public static class SampleServiceFactory
{
    public static ISampleService Create<T>(string clientType, string[] args) where T : class
    {
        return new SampleService<T>(clientType, args);
    }
}