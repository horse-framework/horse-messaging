using System;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Playground
{
    [QueueId(123)]
    public class DemoClass
    {
        public int Type { get; set; }
    }

    public class DemoConsumer : IQueueConsumer<DemoClass>
    {
        public async Task Consume(TmqMessage message, DemoClass model)
        {
            throw new System.NotImplementedException();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            MessageConsumer consumer = MessageConsumer.JsonConsumer();

            consumer.RegisterAssemblyConsumers(typeof(Program));
        }
    }
}