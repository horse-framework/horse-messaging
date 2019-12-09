using System;
using System.Threading;
using System.Threading.Tasks;
using Sample.Mq.Models;
using Sample.Mq.Server;
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
            MqServerOptions mqOptions = new MqServerOptions();
            mqOptions.AllowMultipleQueues = true;
            mqOptions.UseMessageId = true;
            mqOptions.AllowedContentTypes = new[] {ModelTypes.ProducerEvent};

            MqServer server = new MqServer(serverOptions, mqOptions, new ClientAuthenticator(), new Authorization());
            server.SetDefaultDeliveryHandler(new DeliveryHandler());
            server.SetDefaultChannelHandler(new ChannelHandler(), new ChannelAuthenticator());

            server.Start();

            Channel ackChannel = server.CreateChannel("ack-channel");
            ackChannel.Options.RequestAcknowledge = true;
            ackChannel.Options.MessageQueuing = true;
            ackChannel.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            ChannelQueue queue = ackChannel.CreateQueue(ModelTypes.ProducerEvent).Result;
            queue.Options.MessageQueuing = true;

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