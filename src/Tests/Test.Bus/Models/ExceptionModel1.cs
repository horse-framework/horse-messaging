using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [QueueName("ex-queue-1")]
    [RouterName("ex-route-1")]
    public class ExceptionModel1 : ITransportableException
    {
        public void Initialize(ExceptionContext context)
        {
        }
    }
}