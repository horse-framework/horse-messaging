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
        public async Task ReloadAfterRestart()
        {
            await Task.Delay(500);
            ConfigurationFactory.Destroy();
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

            HorseServer server = new HorseServer();

            HorseRider rider = server.UseRider(cfg => cfg.ConfigureQueues(c =>
            {
                c.UsePersistentQueues(
                    cx => { cx.UseInstantFlush().SetAutoShrink(true, TimeSpan.FromSeconds(60)); },
                    q =>
                    {
                        q.Options.Acknowledge = QueueAckDecision.None;
                        q.Options.CommitWhen = CommitWhen.None;
                    },
                    true);
            }));

            HorseQueue queue = await rider.Queue.Create("reload-test", o => o.Type = QueueType.Push);

            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "reload-test");
            msg.SetMessageId("id");
            msg.SetStringContent("Hello, World!");
            await queue.Push(msg);

            QueueMessage queueMsg = queue.Manager.MessageStore.ReadFirst();

            PersistentQueueManager manager = (PersistentQueueManager) queue.Manager;
            await manager.DeliveryHandler.BeginSend(queue, queueMsg);

            await manager.RedeliveryService.Close();
            ConfigurationFactory.Destroy();

            await Task.Delay(1000);

            server = new HorseServer();
            rider = server.UseRider(cfg => cfg.ConfigureQueues(c =>
            {
                c.UsePersistentQueues(
                    c => { c.UseInstantFlush().SetAutoShrink(true, TimeSpan.FromSeconds(60)); },
                    q =>
                    {
                        q.Options.Acknowledge = QueueAckDecision.None;
                        q.Options.CommitWhen = CommitWhen.None;
                    },
                    true);
            }));
            HorseQueue queue2 = rider.Queue.Find("reload-test");
            Assert.NotNull(queue2);
            Assert.NotEqual(0, queue2.Manager.MessageStore.Count());
            QueueMessage loadedMsg = queue2.Manager.MessageStore.ReadFirst();

            Assert.NotNull(loadedMsg);
            Assert.Equal(1, loadedMsg.DeliveryCount);
        }
    }
}