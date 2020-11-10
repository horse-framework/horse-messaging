using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;

namespace Test.Bus.Models
{
    [QueueName("ex-queue-2")]
    [RouterName("ex-route-2")]
    public class ExceptionModel2 : ITransportableException
    {
        public void Initialize(ExceptionContext context)
        {
        }
    }
}