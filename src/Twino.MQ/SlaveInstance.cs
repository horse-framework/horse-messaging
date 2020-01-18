using System;
using Twino.MQ.Clients;

namespace Twino.MQ
{
    internal class SlaveInstance
    {
        public string RemoteHost { get; set; }
        public DateTime ConnectedDate { get; set; }
        public MqClient Client { get; set; }
    }
}