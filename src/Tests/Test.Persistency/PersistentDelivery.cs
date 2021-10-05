using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Handlers;
using Horse.Server;
using Xunit;

namespace Test.Persistency
{
    public class PersistentDelivery
    {
        [Fact]
        public async Task InPersistentHandler()
        {
            ConfigurationFactory.Destroy();
            HorseServer server = new HorseServer();
            HorseRider rider = server.UseRider(cfg => cfg
                                                   .ConfigureQueues(q =>
                                                   {
                                                       q.UsePersistentDeliveryHandler(q =>
                                                                                      {
                                                                                          q.UseInstantFlush()
                                                                                              .KeepLastBackup()
                                                                                              .SetAutoShrink(true, TimeSpan.FromSeconds(60))
                                                                                              .UseInstantFlush();
                                                                                      },
                                                                                      DeleteWhen.AfterSend,
                                                                                      CommitWhen.None,
                                                                                      true);
                                                   }));
            
            HorseQueue queue = await rider.Queue.Create("test");

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, "test");
            message.SetMessageId("id");
            message.SetStringContent("Hello, World!");
            QueueMessage queueMessage = new QueueMessage(message);

            PersistentDeliveryHandler handler = (PersistentDeliveryHandler) queue.DeliveryHandler;
            await handler.BeginSend(queue, queueMessage);

            List<KeyValuePair<string, int>> deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            string header = message.FindHeader(HorseHeaders.DELIVERY);
            Assert.Null(header);

            await handler.BeginSend(queue, queueMessage);
            deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(2, deliveries[0].Value);

            header = message.FindHeader(HorseHeaders.DELIVERY);
            Assert.NotNull(header);
            Assert.Equal(2, Convert.ToInt32(header));

            queueMessage.MarkAsSent();

            await handler.EndSend(queue, queueMessage);
            deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Empty(deliveries);
            server.Stop();
        }
    }
}