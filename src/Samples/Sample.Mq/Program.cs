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
            serverOptions.Hosts[0].Port = 83;
            MqServerOptions mqOptions = new MqServerOptions();
            mqOptions.AllowMultipleQueues = true;
            mqOptions.UseMessageId = true;
            mqOptions.AllowedContentTypes = new[] {ContentTypes.ProducerEvent};

            MqServer server = new MqServer(serverOptions, mqOptions, new ClientAuthenticator(), new Authorization());
            server.SetDefaultDeliveryHandler(new DeliveryHandler());
            server.SetDefaultChannelHandler(new ChannelHandler(), new ChannelAuthenticator());
            server.Start();
            
            ChannelOptions chqOptions = new ChannelOptions();
            Channel chq = server.CreateChannel("ch-queue",
                                               chqOptions,
                                               server.DefaultChannelAuthenticator,
                                               server.DefaultChannelEventHandler,
                                               server.DefaultDeliveryHandler);
            
            chq.CreateQueue(ContentTypes.ProducerEvent);
            Console.WriteLine("Server started");

            TmqClient client = new TmqClient();
            client.ClientId = "test-client";
            client.Connect("tmq://localhost:83");
            client.AcknowledgeTimeout = TimeSpan.FromSeconds(180);
            client.ResponseTimeout = TimeSpan.FromSeconds(300);
            Console.ReadLine();

            bool joined = client.Join("demo", true).Result;
            Console.WriteLine(joined);

            MessageReader reader = new MessageReader((mx, type) => { return Newtonsoft.Json.JsonConvert.DeserializeObject(mx.ToString(), type); });
            reader.On<HelloModel>("demo", 100, m => { Console.WriteLine("Message received: " + m.M); });
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