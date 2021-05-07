using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;
using Test.Client.Models;

namespace Test.Client.Handlers
{
    [AutoResponse(AutoResponse.All)]
    public class DirectHandler : IDirectMessageHandler<ModelC>
    {
        public Task Handle(HorseMessage message, ModelC model, HorseClient client)
        {
            return Task.CompletedTask;
        }
    }
}