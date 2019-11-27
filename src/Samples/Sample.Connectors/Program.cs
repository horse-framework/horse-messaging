using System;
using Twino.Client;
using Twino.Client.Connectors;

namespace Sample.Connectors
{
    class Program
    {
        static void Main(string[] args)
        {
            StickyConnector connector = new StickyConnector(TimeSpan.FromMilliseconds(20));
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

        private static void Connector_Disconnected(TwinoClient client)
        {
            Console.WriteLine("dc");
        }

        private static void Connector_Connected(TwinoClient client)
        {
            Console.WriteLine("c");
        }
    }
}