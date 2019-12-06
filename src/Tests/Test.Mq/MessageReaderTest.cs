using System;
using System.IO;
using System.Text;
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
        public async Task ClientReadsMessagesFromMultipleChannels()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42802);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42802");
            Assert.True(client.IsConnected);

            bool joined = await client.Join("ch-1", true);
            Assert.True(joined);
            joined = await client.Join("ch-0", true);
            Assert.True(joined);

            await Task.Delay(250);

            bool ch0 = false;
            bool ch1 = false;
            MessageReader reader = MessageReader.JsonReader();
            reader.On<MessageA>("ch-0", MessageA.ContentType, a => ch0 = true);
            reader.On<MessageA>("ch-1", MessageA.ContentType, a => ch1 = true);
            reader.Attach(client);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageA("Ax"))));

            bool sent = await client.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.True(sent);
            sent = await client.Push("ch-0", MessageA.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1000);
            Assert.True(ch0);
            Assert.True(ch1);
        }

        /// <summary>
        /// Client reads messages from multiple queues in same channel
        /// </summary>
        [Fact]
        public async Task ClientReadsMessagesFromMultipleQueues()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42803);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42803");
            Assert.True(client.IsConnected);

            bool joined = await client.Join("ch-1", true);
            Assert.True(joined);

            await Task.Delay(250);

            bool ma = false;
            bool mc = false;
            MessageReader reader = MessageReader.JsonReader();
            reader.On<MessageA>("ch-1", MessageA.ContentType, a => ma = true);
            reader.On<MessageA>("ch-1", MessageC.ContentType, c => mc = true);
            reader.Attach(client);

            MemoryStream astream = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageA("Ax"))));
            MemoryStream cstream = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageC("Cx", "x"))));

            bool sent = await client.Push("ch-1", MessageA.ContentType, astream, false);
            Assert.True(sent);
            sent = await client.Push("ch-1", MessageC.ContentType, cstream, false);
            Assert.True(sent);

            await Task.Delay(1000);
            Assert.True(ma);
            Assert.True(mc);
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
        public async Task ExceptionOnBindMethod()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42805);
            server.Start();

            bool thrown = false;
            MessageReader reader = MessageReader.JsonReader();
            reader.OnException += (tm, e) => thrown = true;
            reader.On<MessageA>("ch-1", MessageA.ContentType, a => throw new InvalidOperationException());

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42805");
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

            await Task.Delay(1500);
            Assert.True(client.IsConnected);
            Assert.True(thrown);
        }
    }
}