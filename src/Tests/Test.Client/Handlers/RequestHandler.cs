using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;
using Test.Client.Models;

namespace Test.Client.Handlers
{
    [DirectContentType(300)]
    public class RequestHandler : IHorseRequestHandler<ModelC, ModelA>
    {
        public Task<ModelA> Handle(ModelC request, HorseMessage rawMessage, HorseClient client)
        {
            return Task.FromResult(new ModelA());
        }

        public Task<ErrorResponse> OnError(Exception exception, ModelC request, HorseMessage rawMessage, HorseClient client)
        {
            return Task.FromResult(new ErrorResponse());
        }
    }
}