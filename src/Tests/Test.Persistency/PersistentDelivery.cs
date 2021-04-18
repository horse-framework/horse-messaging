using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Data;
using Horse.Messaging.Server.Data.Configuration;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
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
            PersistentDeliveryHandler handler = null;
            HorseServer server = new HorseServer();
            HorseMq mq = server.UseHorseMq(cfg => cfg
                                                  .AddPersistentQueues(q => q.KeepLastBackup())
                                                  .UseDeliveryHandler(async builder =>
                                                  {
                                                      DatabaseOptions options = new DatabaseOptions
                                                                                {
                                                                                    Filename = "redelivery-test.tdb",
                                                                                    InstantFlush = true,
                                                                                    CreateBackupOnShrink = false,
                                                                                    ShrinkInterval = TimeSpan.FromSeconds(60)
                                                                                };

                                                      handler = new PersistentDeliveryHandler(builder.Queue,
                                                                                              options,
                                                                                              DeleteWhen.AfterSend,
                                                                                              ProducerAckDecision.None,
                                                                                              true);
                                                      await handler.Initialize();
                                                      return handler;
                                                  }));

            HorseQueue queue = await mq.CreateQueue("test");

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, "test");
            message.SetMessageId("id");
            message.SetStringContent("Hello, World!");
            QueueMessage queueMessage = new QueueMessage(message);

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
        }
    }
}