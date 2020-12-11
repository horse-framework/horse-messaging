using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;

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