using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Delivery;
using Twino.MQ.Handlers;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(new SendAckDeliveryHandler(AcknowledgeWhen.AfterReceived));
            TwinoServer server = new TwinoServer();
            server.UseMqServer(mq);

            mq.AddPersistentQueues(cfg => cfg.UseAutoFlush()
                                             .KeepLastBackup());

            await mq.LoadPersistentQueues();

            //await mq.FindChannel("123").CreatePersistentQueue(123, DeleteWhen.AfterAcknowledgeReceived, DeliveryAcknowledgeDecision.IfSaved);

            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
}