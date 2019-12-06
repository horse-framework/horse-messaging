using System;
using System.IO;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    public class MessageReaderTest
    {
        /// <summary>
        /// Clients subscribes to a queue and reads message with message reader
        /// </summary>
        [Fact]
        public async Task ClientReadsMessageFromQueue()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42801);
            server.Start();

            bool received = false;
            MessageReader reader = MessageReader.JsonReader();
            reader.On<MessageA>("ch-1", MessageA.ContentType, a => { received = true; });

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42801");
            Assert.True(client.IsConnected);
            reader.Attach(client);

            bool joined = await client.Join("ch-1", true);
            Assert.True(joined);
            await Task.Delay(1000);

            MessageA m = new MessageA("Msg-A");
            MemoryStream ms = new MemoryStream();
            await System.Text.Json.JsonSerializer.SerializeAsync(ms, m);

            bool sent = await client.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.True(sent);
            
            await Task.Delay(500);
            Assert.True(received);
        }

        /// <summary>
        /// Client reads messages from multiple channels
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleChannels()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42802);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple queues in same channel
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleQueues()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42803);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Reader attaches multiple clients
        /// </summary>
        [Fact]
        public void MultipleAttachOnSameReader()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42804);

            throw new NotImplementedException();
        }

        /// <summary>
        /// End user throws an unhandled exception in attached method
        /// </summary>
        [Fact]
        public void ExceptionOnBindMethod()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42805);

            throw new NotImplementedException();
        }
    }
}