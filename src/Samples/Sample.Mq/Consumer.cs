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
            _connector = new TmqStickyConnector(TimeSpan.FromSeconds(5));
        }

        public void Start()
        {
            _connector.AddProperty(TmqHeaders.CLIENT_TYPE, "consumer");
            _connector.AddProperty(TmqHeaders.CLIENT_TOKEN, "anonymous");
            _connector.AddHost("tmq://localhost:48050");

            _connector.Connected += Connected;
            _connector.MessageReceived += MessageReceived;
            _connector.Run();

            _timer = new Timer(o =>
            {
                //send request
            }, null, 6000, 6000);
        }

        private void Connected(SocketBase client)
        {
            TmqClient tc = (TmqClient) client;
            tc.AutoAcknowledge = true;
            
            //subscribe to queues (ack and non-ack)
        }

        private void MessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            //queue messages will trigger here
        }
    }
}