using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
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
                    q.UsePersistentQueues(q =>
                        {
                            q.UseInstantFlush()
                                .KeepLastBackup()
                                .SetAutoShrink(true, TimeSpan.FromSeconds(60));
                        },
                        c =>
                        {
                            c.Options.CommitWhen = CommitWhen.AfterSent;
                            c.Options.PutBack = PutBackDecision.No;
                        },
                        true);
                }));

            string queueName = $"test{Environment.TickCount}";

            HorseQueue queue = await rider.Queue.Create(queueName);

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, queueName);
            message.SetMessageId("id");
            message.SetStringContent("Hello, World!");
            QueueMessage queueMessage = new QueueMessage(message);

            PersistentQueueManager manager = queue.Manager as PersistentQueueManager;
            await manager.DeliveryHandler.BeginSend(queue, queueMessage);

            List<KeyValuePair<string, int>> deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            string header = message.FindHeader(HorseHeaders.DELIVERY);
            Assert.Null(header);

            await manager.DeliveryHandler.BeginSend(queue, queueMessage);
            deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(2, deliveries[0].Value);

            header = message.FindHeader(HorseHeaders.DELIVERY);
            Assert.NotNull(header);
            Assert.Equal(2, Convert.ToInt32(header));

            queueMessage.MarkAsSent();

            await manager.DeliveryHandler.EndSend(queue, queueMessage);
            await manager.RemoveMessage(queueMessage);

            deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Empty(deliveries);
            server.Stop();
        }
    }
}