using System;
using System.Threading;
using Twino.Client.TMQ.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Sample.Mq
{
    public class Producer
    {
        private Timer _timer;
        private readonly TmqAbsoluteConnector _connector;

        public Producer()
        {
            _connector = new TmqAbsoluteConnector(TimeSpan.FromSeconds(5));
        }

        public void Start()
        {
            _connector.AddProperty(TmqHeaders.CLIENT_TYPE, "producer");
            _connector.AddProperty(TmqHeaders.CLIENT_TOKEN, "S3cr37_pr0duc3r_t0k3n");
            _connector.AddHost("tmq://localhost:48050");

            _connector.Connected += Connected;
            _connector.MessageReceived += MessageReceived;
            _connector.Run();

            _timer = new Timer(o =>
            {
                //push to ack queue
                //push to non-ack queue
            }, null, 6000, 6000);
        }

        private void Connected(SocketBase client)
        {
            //create ack channel
            //create non ack channel
        }

        private void MessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
        }
    }
}