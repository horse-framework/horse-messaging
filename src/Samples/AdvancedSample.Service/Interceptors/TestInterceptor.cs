using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Service.Interceptors;

internal class TestInterceptor : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client)
    {
        Console.WriteLine("INTERCEPT");
        return Task.CompletedTask;
    }
}