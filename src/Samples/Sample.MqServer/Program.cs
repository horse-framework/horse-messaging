using System;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.Server;

namespace Sample.MqServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerOptions serverOptions = ServerOptions.CreateDefault();
            serverOptions.Hosts[0].Port = 83;
            MqServerOptions mqOptions = new MqServerOptions();
            mqOptions.AllowMultipleQueues = true;
            mqOptions.UseMessageId = true;
            mqOptions.AllowedContentTypes = new ushort[] {100, 101, 102};

            MQServer server = new MQServer(serverOptions, mqOptions, new ClientAuthenticator(), new Authorization());
            server.SetDefaultDeliveryHandler(new DeliveryHandler());
            server.SetDefaultChannelHandler(new ChannelHandler(), new ChannelAuthenticator());

            server.Start();
            Channel demoChannel = server.CreateChannel("demo");
            demoChannel.CreateQueue(100);
            Console.WriteLine("Server started");

            TmqClient client = new TmqClient();
            client.Data.Method = "GET";
            client.Data.Path = "/";
            client.Data.Properties.Add("Client-Id", "test-client");
            client.Connect("tmq://localhost:83");
            
            server.Server.BlockWhileRunning();
        }
    }
}