﻿using Horse.Messaging.Server;
using Horse.Messaging.Server.Data;
using Horse.Messaging.Server.Queues;
using Horse.Server;

namespace Sample.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            HorseMq mq = HorseMqBuilder.Create()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .AddClientHandler<ClientHandler>()
                                       .AddQueueEventHandler<QueueEventHandler>()
                                       .AddPersistentQueues()
                                       .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterSaved)
                                       .Build();

            mq.LoadPersistentQueues();

            HorseServer server = new HorseServer();
            server.UseHorseMq(mq);
            server.Run(9999);
        }
    }
}