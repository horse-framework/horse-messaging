using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Data.Configuration;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Twino.Server;
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
            TwinoServer server = new TwinoServer();
            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
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

            TwinoQueue queue = await mq.CreateQueue("test");

            TwinoMessage message = new TwinoMessage(MessageType.QueueMessage, "test");
            message.SetMessageId("id");
            message.SetStringContent("Hello, World!");
            QueueMessage queueMessage = new QueueMessage(message);

            await handler.BeginSend(queue, queueMessage);

            List<KeyValuePair<string, int>> deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            string header = message.FindHeader(TwinoHeaders.DELIVERY);
            Assert.Null(header);

            await handler.BeginSend(queue, queueMessage);
            deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("id", deliveries[0].Key);
            Assert.Equal(2, deliveries[0].Value);

            header = message.FindHeader(TwinoHeaders.DELIVERY);
            Assert.NotNull(header);
            Assert.Equal(2, Convert.ToInt32(header));

            queueMessage.MarkAsSent();

            await handler.EndSend(queue, queueMessage);
            deliveries = handler.RedeliveryService.GetDeliveries();
            Assert.Empty(deliveries);
        }
    }
}