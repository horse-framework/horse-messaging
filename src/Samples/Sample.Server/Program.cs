using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Handlers;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static Task Main(string[] args)
        {
            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(new SendAckDeliveryHandler(AcknowledgeWhen.AfterAcknowledge));
            mq.Options.AutoChannelCreation = true;
            mq.Options.AutoQueueCreation = true;
            mq.Options.RequestAcknowledge = true;
            
            TwinoServer server = new TwinoServer();
            server.UseMqServer(mq);
            server.Start(22200);

            return server.BlockWhileRunningAsync();
        }
    }
}