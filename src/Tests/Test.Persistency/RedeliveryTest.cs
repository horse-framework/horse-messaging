using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
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
            string guid = Guid.NewGuid().ToString();
            await Task.Delay(500);
            RedeliveryService service = new RedeliveryService($"data/reload-test-{guid}.tdb.delivery");
            await service.Load();
            await service.Clear();
            await service.Set("id", 4);
            await service.Close();

            HorseServer server = new HorseServer();
            HorseRider rider = server.UseRider(cfg => cfg
                .ConfigureChannels(c => { c.UseCustomPersistentConfigurator(null); })
                .ConfigureQueues(c =>
                {
                    c.UseCustomPersistentConfigurator(new QueueOptionsConfigurator(c.Rider, $"queues-{guid}.json"));
                    c.UsePersistentQueues(
                        cx =>
                        {
                            cx.SetPhysicalPath(q => $"{q.Name}-{guid}.tdb");
                            cx.UseInstantFlush().SetAutoShrink(true, TimeSpan.FromSeconds(60));
                        },
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

            await Task.Delay(1000);

            server = new HorseServer();
            rider = server.UseRider(cfg => cfg
                .ConfigureChannels(c => { c.UseCustomPersistentConfigurator(null); })
                .ConfigureQueues(c =>
                {
                    c.UseCustomPersistentConfigurator(new QueueOptionsConfigurator(c.Rider, $"queues-{guid}.json"));
                    c.UsePersistentQueues(
                        c =>
                        {
                            c.SetPhysicalPath(q => $"{q.Name}-{guid}.tdb");
                            c.UseInstantFlush().SetAutoShrink(true, TimeSpan.FromSeconds(60));
                        },
                        q =>
                        {
                            q.Options.Acknowledge = QueueAckDecision.None;
                            q.Options.CommitWhen = CommitWhen.None;
                        },
                        true);
                }));
            await Task.Delay(250);
            HorseQueue queue2 = rider.Queue.Find("reload-test");
            Assert.NotNull(queue2);
            Assert.NotEqual(0, queue2.Manager.MessageStore.Count());
            QueueMessage loadedMsg = queue2.Manager.MessageStore.ReadFirst();

            Assert.NotNull(loadedMsg);
            Assert.Equal(1, loadedMsg.DeliveryCount);
        }
    }
}