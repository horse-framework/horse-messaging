using System;
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

            MQServer server = new MQServer(serverOptions, mqOptions);
            server.SetDefaultDeliveryHandler(new DeliveryHandler());
            server.Start();
            Channel demoChannel = server.CreateChannel("demo");
            demoChannel.CreateQueue(100);
            server.Server.BlockWhileRunning();
        }
    }
}