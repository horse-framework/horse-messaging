using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Queues;
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
                                                  .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived));

            await mq.LoadPersistentQueues();

            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
}