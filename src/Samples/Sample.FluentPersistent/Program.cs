using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Delivery;
using Twino.MQ.Handlers;
using Twino.Server;

namespace Sample.FluentPersistent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(new JustAllowDeliveryHandler());
            mq.AddPersistentQueues(cfg => cfg.KeepLastBackup());
            await mq.LoadPersistentQueues();
            await mq.CreatePersistentQueue("test", 0, DeleteWhen.AfterAcknowledgeReceived, DeliveryAcknowledgeDecision.IfSaved);

            TwinoServer server = new TwinoServer();
            server.UseMqServer(mq);
            server.Start(8000);
            await server.BlockWhileRunningAsync();
        }
    }
}