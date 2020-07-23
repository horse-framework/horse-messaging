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
    /// <summary>
    /// Ports 42800 - 42810
    /// </summary>
    public class MessageConsumerTest
    {
        /// <summary>
        /// Clients subscribes to a queue and reads message with message reader
        /// 42800 - 42810
        /// </summary>
        [Fact]
        public async Task ClientReadsMessageFromQueue()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42801);
            server.Start();

            bool received = false;
            MessageConsumer consumer = MessageConsumer.JsonConsumer();
            consumer.On<MessageA>("ch-1", MessageA.ContentType, a => { received = true; });

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42801");
            Assert.True(client.IsConnected);
            consumer.Attach(client);

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);
            await Task.Delay(1000);

            MessageA m = new MessageA("Msg-A");
            MemoryStream ms = new MemoryStream();
            await System.Text.Json.JsonSerializer.SerializeAsync(ms, m);

            TwinoResult sent = await client.Queues.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);

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

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);
            joined = await client.Channels.Join("ch-0", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(250);

            bool ch0 = false;
            bool ch1 = false;
            MessageConsumer consumer = MessageConsumer.JsonConsumer();
            consumer.On<MessageA>("ch-0", MessageA.ContentType, a => ch0 = true);
            consumer.On<MessageA>("ch-1", MessageA.ContentType, a => ch1 = true);
            consumer.Attach(client);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageA("Ax"))));

            TwinoResult sent = await client.Queues.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
            sent = await client.Queues.Push("ch-0", MessageA.ContentType, ms, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);

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

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(250);

            bool ma = false;
            bool mc = false;
            MessageConsumer consumer = MessageConsumer.JsonConsumer();
            consumer.On<MessageA>("ch-1", MessageA.ContentType, a => ma = true);
            consumer.On<MessageA>("ch-1", MessageC.ContentType, c => mc = true);
            consumer.Attach(client);

            MemoryStream astream = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageA("Ax"))));
            MemoryStream cstream = new MemoryStream(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new MessageC("Cx", "x"))));

            TwinoResult sent = await client.Queues.Push("ch-1", MessageA.ContentType, astream, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
            sent = await client.Queues.Push("ch-1", MessageC.ContentType, cstream, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);

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
            MessageConsumer consumer = MessageConsumer.JsonConsumer();
            consumer.OnException += (tm, e) => thrown = true;
            consumer.On<MessageA>("ch-1", MessageA.ContentType, a => throw new InvalidOperationException());

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42805");
            Assert.True(client.IsConnected);
            consumer.Attach(client);

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);
            await Task.Delay(1000);

            MessageA m = new MessageA("Msg-A");
            MemoryStream ms = new MemoryStream();
            await System.Text.Json.JsonSerializer.SerializeAsync(ms, m);

            TwinoResult sent = await client.Queues.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);

            await Task.Delay(1500);
            Assert.True(client.IsConnected);
            Assert.True(thrown);
        }

        /// <summary>
        /// Uses OnDirect and OffDirect methods
        /// </summary>
        [Fact]
        public async Task ConsumeDirectMessages()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42806);
            server.Start();

            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            client1.AutoAcknowledge = true;

            await client1.ConnectAsync("tmq://localhost:42806");
            Assert.True(client1.IsConnected);

            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:42806");
            Assert.True(client2.IsConnected);

            bool received = false;
            MessageConsumer consumer = MessageConsumer.JsonConsumer();
            consumer.OnDirect<MessageA>(MessageA.ContentType, a => received = true);
            consumer.Attach(client1);

            MessageA m = new MessageA("Msg-A");
            var sent = await client2.SendJsonAsync(MessageType.DirectMessage, "client-1", MessageA.ContentType, m, true);

            Assert.Equal(TwinoResultCode.Ok, sent.Code);
            await Task.Delay(100);
            Assert.True(received);
        }
    }
}