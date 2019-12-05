using System;
using Twino.Client;
using Twino.Client.Connectors;
using Twino.Client.WebSocket;
using Twino.Client.WebSocket.Connectors;
using Twino.Core;

namespace Sample.Connectors
{
    class Program
    {
        static void Main(string[] args)
        {
            WsStickyConnector connector = new WsStickyConnector(TimeSpan.FromMilliseconds(20));
            connector.AddHost("ws://127.0.0.1");
            connector.Connected += Connector_Connected;
            connector.Disconnected += Connector_Disconnected;
            connector.Run();

            while (true)
            {
                connector.Send("Hello world!");
                Console.ReadLine();
            }
        }

        private static void Connector_Disconnected(SocketBase client)
        {
            Console.WriteLine("dc");
        }

        private static void Connector_Connected(SocketBase client)
        {
            Console.WriteLine("c");
        }
    }
}