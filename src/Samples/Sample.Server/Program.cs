using System.Threading.Tasks;
using Twino.MQ;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static Task Main(string[] args)
        {
            TwinoMQ mq = TwinoMqBuilder.Create()
                                       .AddClientHandler<ClientHandler>()
                                       .AddQueueEventHandler<QueueEventHandler>()
                                       .UseJustAllowDeliveryHandler()
                                       .Build();
            
            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(mq);
            server.Start(26222);
            return server.BlockWhileRunningAsync();
        }
    }
}