using System;
using System.Collections.Generic;
using System.Linq;
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
    public class RedeliveryTest
    {
        [Fact]
        public async Task ServiceMethods()
        {
            RedeliveryService service = new RedeliveryService("1.delivery");
            await service.Load();
            await service.Clear();

            List<KeyValuePair<string, int>> deliveries = service.GetDeliveries();
            Assert.Empty(deliveries);

            await service.Set("msg1", 1);
            deliveries = service.GetDeliveries();
            Assert.NotEmpty(deliveries);
            Assert.Equal("msg1", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            await service.Set("msg1", 2);
            deliveries = service.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal(2, deliveries[0].Value);

            await service.Remove("msg1");
            deliveries = service.GetDeliveries();
            Assert.Empty(deliveries);

            await service.Close();
            service.Delete();
        }

        [Fact]
        public async Task InPersistentHandler()
        {
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

        [Fact]
        public async Task ReloadAfterRestart()
        {
            RedeliveryService service = new RedeliveryService("data/reload-test.tdb.delivery");
            await service.Load();
            await service.Clear();
            await service.Set("id", 4);
            await service.Close();

            if (System.IO.File.Exists("data/config.json"))
                System.IO.File.Delete("data/config.json");

            if (System.IO.File.Exists("data/reload-test.tdb"))
                System.IO.File.Delete("data/reload-test.tdb");

            if (System.IO.File.Exists("data/reload-test.tdb.delivery"))
                System.IO.File.Delete("data/reload-test.tdb.delivery");

            TwinoServer server = new TwinoServer();
            PersistentDeliveryHandler handler = null;
            Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> fac = async builder =>
            {
                DatabaseOptions options = new DatabaseOptions
                                          {
                                              Filename = "data/reload-test.tdb",
                                              InstantFlush = true,
                                              CreateBackupOnShrink = false,
                                              ShrinkInterval = TimeSpan.FromSeconds(60)
                                          };

                handler = (PersistentDeliveryHandler) await builder.CreatePersistentDeliveryHandler(o =>
                {
                    return new PersistentDeliveryHandler(builder.Queue,
                                                         options,
                                                         DeleteWhen.AfterSend,
                                                         ProducerAckDecision.None,
                                                         true);
                });
                return handler;
            };

            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
                                                  .AddPersistentQueues(q => q.KeepLastBackup())
                                                  .UseDeliveryHandler(fac));

            TwinoQueue queue = await mq.CreateQueue("reload-test",
                                                    o => o.Status = QueueStatus.Push);

            TwinoMessage msg = new TwinoMessage(MessageType.QueueMessage, "reload-test");
            msg.SetMessageId("id");
            msg.SetStringContent("Hello, World!");
            await queue.Push(msg);

            QueueMessage queueMsg = queue.Messages.FirstOrDefault();
            await handler.BeginSend(queue, queueMsg);

            await handler.RedeliveryService.Close();
            ConfigurationFactory.Destroy();

            mq = server.UseTwinoMQ(cfg => cfg
                                          .AddPersistentQueues(q => q.KeepLastBackup())
                                          .UseDeliveryHandler(fac));

            await mq.LoadPersistentQueues();
            TwinoQueue queue2 = mq.FindQueue("reload-test");
            Assert.NotNull(queue2);
            Assert.NotEmpty(queue2.Messages);
            QueueMessage loadedMsg = queue2.Messages.FirstOrDefault();
            Assert.NotNull(loadedMsg);
            Assert.Equal(1, loadedMsg.DeliveryCount);
        }
    }
}