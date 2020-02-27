using Sample.Mq.Server;
using System;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.Server;

namespace Sample.Mq
{
    public class HelloModel
    {
        public string M { get; set; }
    }

    class Program
    {
        private static MqServer _server;

        static void StartServer()
        {
            ServerOptions serverOptions = ServerOptions.CreateDefault();
            serverOptions.Hosts[0].Port = 48050;

            ServerBuilder builder = new ServerBuilder();
            builder.LoadFromFile("options.json");

            builder.AddAuthenticator(new ClientAuthenticator());
            builder.AddAuthorization(new Authorization());
            builder.AddDefaultDeliveryHandler(new DeliveryHandler());
            builder.AddDefaultChannelHandler(new ChannelHandler());
            builder.AddDefaultChannelAuthenticator(new ChannelAuthenticator());

            TwinoServer twinoServer = new TwinoServer(serverOptions);

            MqServer server = builder.CreateServer();

            twinoServer.UseMqServer(server);
            twinoServer.Start();

            Console.WriteLine("Server started");
            _server = server;
        }

        static void Main(string[] args)
        {
            StartServer();

            ThreadPool.QueueUserWorkItem(async s =>
            {
                await Task.Delay(1000);
                Producer producer = new Producer();
                producer.Start();
            }, null);


            ThreadPool.QueueUserWorkItem(async s =>
            {
                await Task.Delay(5000);
                Consumer consumer = new Consumer();
                consumer.Start();
            }, null);

            //all operations are async, do not let the application to close
            _server.Server.BlockWhileRunning();
        }
    }
}