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
            TwinoServer server = new TwinoServer();
            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
                                                  .AddPersistentQueues(q => q.UseAutoFlush().KeepLastBackup())
                                                  .UseSendAckDeliveryHandler(AcknowledgeWhen.AfterReceived));

            await mq.LoadPersistentQueues();

            //await mq.FindChannel("123").CreatePersistentQueue(123, DeleteWhen.AfterAcknowledgeReceived, DeliveryAcknowledgeDecision.IfSaved);

            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
}