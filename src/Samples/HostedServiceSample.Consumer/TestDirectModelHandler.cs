using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;

namespace HostedServiceSample.Consumer
{
    [DirectContentType(1)]
    public class TestDirectModelHandler : IDirectMessageHandler<TestDirectModel>
    {
        readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        public Task Handle(HorseMessage message, TestDirectModel model, HorseClient client)
        {
            _ = Console.Out.WriteLineAsync("Consumed!!!");
            _ = Console.Out.WriteLineAsync(JsonSerializer.Serialize(model, _options));
            return Task.CompletedTask;
        }
    }
}