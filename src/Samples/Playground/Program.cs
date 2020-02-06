using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.MQ;
using Twino.MQ.Handlers;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit.Sdk;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            MqServer server = new MqServer();
            server.SetDefaultDeliveryHandler(new JustAllowDeliveryHandler());

            Channel ch = server.CreateChannel("channelName");
            ChannelQueue q = ch.CreateQueue(123).Result;
        }
    }
}