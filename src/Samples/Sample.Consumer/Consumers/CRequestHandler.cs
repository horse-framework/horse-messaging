using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    public class CRequestHandler : ITwinoRequestHandler<ModelC, ModelA>
    {
        public Task<ModelA> Handle(ModelC request, TmqMessage rawMessage, TmqClient client)
        {
            Console.WriteLine("Model C consumed");
            return Task.FromResult(new ModelA {Foo = "response-a"});
        }

        public async Task<ErrorResponse<ModelA>> OnError(Exception exception, ModelC request, TmqMessage rawMessage, TmqClient client)
        {
            throw new NotImplementedException();
        }
    }
}