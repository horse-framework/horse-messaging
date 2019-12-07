using System;
using Twino.Client.TMQ.Connectors;

namespace Sample.Mq
{
    public class Producer
    {
        private TmqAbsoluteConnector _connector;

        public Producer()
        {
            _connector = new TmqAbsoluteConnector(TimeSpan.FromSeconds(5));
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

    }
}