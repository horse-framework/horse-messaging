using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Queues;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoMQ mq = TwinoMqBuilder.Create()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .AddClientHandler<ClientHandler>()
                                       .AddQueueEventHandler<QueueEventHandler>()
                                       .AddPersistentQueues()
                                       .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterSaved)
                                       .Build();

            mq.LoadPersistentQueues();

            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(mq);
            server.Run(26222);
        }
    }
}