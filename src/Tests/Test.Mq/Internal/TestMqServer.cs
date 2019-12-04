using System;
using Twino.MQ;

namespace Test.Mq.Internal
{
    internal class TestMqServer
    {
        public MQServer Server { get; private set; }

        public int OnQueueCreated { get; set; }
        public int OnQueueRemoved { get; set; }
        public int ClientJoined { get; set; }
        public int ClientLeft { get; set; }
        public int OnChannelStatusChanged { get; set; }
        public int OnQueueStatusChanged { get; set; }

        public int OnReceived { get; set; }
        public int OnSendStarting { get; set; }
        public int OnBeforeSend { get; set; }
        public int OnAfterSend { get; set; }
        public int OnSendCompleted { get; set; }
        public int OnAcknowledge { get; set; }
        public int OnTimeUp { get; set; }
        public int OnAcknowledgeTimeUp { get; set; }
        public int OnRemove { get; set; }
        public int OnException { get; set; }
        public int SaveMessage { get; set; }

        internal void Run(int port)
        {
            throw new NotImplementedException();
        }
    }
}