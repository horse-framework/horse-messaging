using Sample.Mq.Models;
using System;
using System.Threading;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Sample.Mq
{
    public class Consumer
    {
        private Timer _timer;
        private readonly TmqStickyConnector _connector;

        public Consumer()
        {
            _connector = new TmqStickyConnector(TimeSpan.FromSeconds(5), () =>
            {
                TmqClient client = new TmqClient();
                client.ClientId = "consumer-id";
                client.SetClientType("consumer");
                client.SetClientToken("anonymous");
                client.AutoAcknowledge = true;

                return client;
            });
        }

        public void Start()
        {
            _connector.AddHost("tmq://localhost:48050");

            _connector.Connected += Connected;
            _connector.MessageReceived += MessageReceived;
            _connector.Run();

            _timer = new Timer(async o =>
            {
                if (_connector.IsConnected)
                {
                    TmqClient client = _connector.GetClient();

                    TmqMessage message = new TmqMessage(MessageType.DirectMessage, "producer-id");
                    message.ContentType = ModelTypes.ConsumerRequest;

                    ConsumerRequest request = new ConsumerRequest();
                    request.Guid = Guid.NewGuid().ToString();

                    await message.SetJsonContent(request);
                    TmqMessage response = await client.Request(message);
                    ProducerResponse rmodel = await response.GetJsonContent<ProducerResponse>();
                    Console.WriteLine($"> response received for: {rmodel.RequestGuid}");
                }
            }, null, 6000, 6000);
        }

        private void Connected(SocketBase client)
        {
            Console.WriteLine("consumer connection established");

            TmqClient tc = (TmqClient)client;
            tc.AutoAcknowledge = true;

            tc.Channels.Join("AckChannel", false);
            tc.Channels.Join("BasicChannel", false);
        }

        private void MessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            switch (message.Type)
            {
                case MessageType.QueueMessage:
                    if (message.ContentType == ModelTypes.ProducerEvent)
                    {
                        ProducerEvent e = message.GetJsonContent<ProducerEvent>().Result;
                        Console.WriteLine(message.Target == "AckChannel"
                                              ? $"> AckChannel received: #{e.No}"
                                              : $"> BasicChannel received: #{e.No}");
                    }

                    break;
            }
        }
    }
}