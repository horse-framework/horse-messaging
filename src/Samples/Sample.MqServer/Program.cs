using System;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Sample.MqServer
{
    public class HelloModel
    {
        public string M { get; set; }
    }

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

            Twino.MQ.MqServer server = new Twino.MQ.MqServer(serverOptions, mqOptions, new ClientAuthenticator(), new Authorization());
            
            server.SetDefaultDeliveryHandler(new DeliveryHandler());
            server.SetDefaultChannelHandler(new ChannelHandler(), new ChannelAuthenticator());

            server.Start();
            Channel demoChannel = server.CreateChannel("demo");
            demoChannel.CreateQueue(100);
            Console.WriteLine("Server started");

            TmqClient client = new TmqClient();
            client.ClientId = "test-client";
            client.Connect("tmq://localhost:83");
            client.AcknowledgeTimeout = TimeSpan.FromSeconds(180);
            client.ResponseTimeout = TimeSpan.FromSeconds(300);
            Console.ReadLine();

            bool joined = client.Join("demo", true).Result;
            Console.WriteLine(joined);

            MessageReader reader = new MessageReader((mx, type) =>
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject(mx.ToString(), type);
            });
            reader.On<HelloModel>("demo", 100, m =>
            {
                Console.WriteLine("Message received: " + m.M);
            });
            reader.Attach(client);

            Console.ReadLine();

            TmqMessage msg = new TmqMessage();
            msg.Type = MessageType.Channel;
            msg.Target = "demo";
            msg.ContentType = 100;
            HelloModel hm = new HelloModel {M = "Hello World"};
            msg.SetStringContent(Newtonsoft.Json.JsonConvert.SerializeObject(hm));
            client.Send(msg);

            server.Server.BlockWhileRunning();
        }
    }
}