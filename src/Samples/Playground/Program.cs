using System;
using System.IO;
using System.Text;
using Twino.Client.WebSocket;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            TmqWriter writer = new TmqWriter();
            TmqReader reader = new TmqReader();

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < 1000; i++)
                builder.AppendLine(new string('x', 50));

            TmqMessage msg = new TmqMessage(MessageType.Client);
            msg.ContentType = 45700;
            msg.SetStringContent(builder.ToString());

            MemoryStream ms = new MemoryStream();
            writer.Write(msg, ms).Wait();

            ms.Position = 0;
            TmqMessage result = reader.Read(ms).Result;
            Console.WriteLine(result.ToString().Length);
        }

        private static void Ws()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < 1; i++)
                builder.AppendLine(new string('x', 50));

            TwinoWebSocket client = new TwinoWebSocket();
            client.Connect("wss://echo.websocket.org");
            client.Disconnected += (c) => Console.WriteLine("disc");
            client.MessageReceived += (c, m) => Console.WriteLine(m.Length);
            Console.ReadLine();
            client.Send("Hello"); // (builder.ToString());
            Console.ReadLine();

            WebSocketWriter writer = new WebSocketWriter();
            WebSocketReader reader = new WebSocketReader();

            WebSocketMessage msg = new WebSocketMessage();
            msg.OpCode = SocketOpCode.UTF8;

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
            msg.Content = ms;

            MemoryStream ms2 = new MemoryStream();
            writer.Write(msg, ms2).Wait();

            ms2.Position = 0;
            WebSocketMessage msg2 = reader.Read(ms2).Result;
            Console.WriteLine(msg2.Length);
        }
    }
}