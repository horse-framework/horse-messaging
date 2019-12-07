using System;
using Twino.Client.TMQ.Connectors;

namespace Sample.Mq
{
    public class Consumer
    {
        private TmqStickyConnector _connector;

        public Consumer()
        {
            _connector = new TmqStickyConnector(TimeSpan.FromSeconds(5));
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}