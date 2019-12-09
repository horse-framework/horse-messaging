using System;
using System.Threading;
using Sample.Mq.Models;
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

                return client;
            });
        }

        public void Start()
        {
            _connector.AddHost("tmq://localhost:48050");

            _connector.Connected += Connected;
            _connector.MessageReceived += MessageReceived;
            _connector.Run();

            _timer = new Timer(o =>
            {
                if (_connector.IsConnected)
                {
                    TmqClient client = _connector.GetClient();

                    TmqMessage message = new TmqMessage(MessageType.Client, "producer-id");
                    message.ContentType = ModelTypes.ConsumerRequest;

                    ConsumerRequest request = new ConsumerRequest();
                    request.Guid = Guid.NewGuid().ToString();

                    message.SetJsonContent(request).Wait();
                    client.Send(message);
                }
            }, null, 6000, 6000);
        }

        private void Connected(SocketBase client)
        {
            Console.WriteLine("consumer connection established");
            
            TmqClient tc = (TmqClient) client;
            tc.AutoAcknowledge = true;

            tc.Join("ack-channel", false);
            tc.Join("channel", false);
        }

        private void MessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Response:
                    if (message.ContentType == ModelTypes.ProducerResponse)
                    {
                        ProducerResponse response = message.GetJsonContent<ProducerResponse>().Result;
                        Console.WriteLine($"response received for: {response.RequestGuid}");
                    }

                    break;

                case MessageType.Channel:
                    if (message.ContentType == ModelTypes.ProducerEvent)
                    {
                        ProducerEvent e = message.GetJsonContent<ProducerEvent>().Result;
                        Console.WriteLine(message.Target == "ack-channel"
                                              ? $"ack channel received: {e.No}"
                                              : $"channel received: {e.No}");
                    }

                    break;
            }
        }
    }
}