using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [QueueName("ex-queue-3")]
    [RouterName("ex-route-3")]
    public class ExceptionModel3 : ITransportableException
    {
        public void Initialize(ExceptionContext context)
        {
        }
    }
}