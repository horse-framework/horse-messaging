using System;
using System.Threading;
using Sample.Mq.Models;
using Sample.Mq.Server;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;
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
            ackChannel.CreateQueue(ModelTypes.ProducerEvent);

            Console.WriteLine("Server started");
            _server = server;
        }

        static void Main(string[] args)
        {
            StartServer();

            Thread.Sleep(2000);
            Producer producer = new Producer();
            producer.Start();

            //wait for first messages of the producer, so we can see queue messages are keeping if there are no receivers or not.
            Thread.Sleep(4000);

            Consumer consumer = new Consumer();
            consumer.Start();

            //all operations are async, do not let the application to close
            _server.Server.BlockWhileRunning();
        }
    }
}