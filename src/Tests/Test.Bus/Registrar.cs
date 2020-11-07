using Test.Bus.Consumers;
using Twino.MQ.Client.Connectors;

namespace Test.Bus
{
    public class Registrar
    {
        public void Register(TmqStickyConnector connector)
        {
            connector.Observer.RegisterConsumer<ExceptionConsumer1>();
            connector.Observer.RegisterConsumer<ExceptionConsumer2>();
            connector.Observer.RegisterConsumer<ExceptionConsumer3>();
            
            connector.Observer.RegisterConsumer<QueueConsumer1>();
            connector.Observer.RegisterConsumer<QueueConsumer2>();
            connector.Observer.RegisterConsumer<QueueConsumer3>();
            connector.Observer.RegisterConsumer<QueueConsumer4>();
            connector.Observer.RegisterConsumer<QueueConsumer5>();

            connector.AutoSubscribe = true;
        }
    }
}