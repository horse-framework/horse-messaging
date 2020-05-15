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
    public class ClientOptionsTest
    {
        /// <summary>
        /// If true, every message must have an id even user does not set
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UseUniqueMessageId(bool enabled)
        {
            int port = enabled ? 42701 : 42702;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Server.Options.UseMessageId = enabled;
            server.Server.FindChannel("ch-1").FindQueue(MessageA.ContentType).Options.UseMessageId = false;
            server.Start();

            TmqClient client = new TmqClient();
            client.UseUniqueMessageId = false;

            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);

            TwinoResult joined = await client.Join("ch-1", true);
            Assert.Equal(TwinoResult.Ok, joined);
            await Task.Delay(250);

            TmqMessage received = null;
            client.MessageReceived += (c, m) => received = m;

            MessageA a = new MessageA("A");
            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(a);
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
            TwinoResult sent = await client.Push("ch-1", MessageA.ContentType, ms, false);
            Assert.Equal(TwinoResult.Ok, sent);

            await Task.Delay(1000);

            Assert.NotNull(received);

            if (enabled)
                Assert.NotNull(received.MessageId);
            else
                Assert.Null(received.MessageId);
        }

        /// <summary>
        /// Subscribes message received event of TmqClient.
        /// Sends a message and waits for response.
        /// If catching response is enabled, response message should trigger message received event.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CatchResponseMessages(bool enabled)
        {
            int port = enabled ? 42711 : 42712;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client1.CatchResponseMessages = enabled;

            await client1.ConnectAsync("tmq://localhost:" + port);
            await client2.ConnectAsync("tmq://localhost:" + port);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool responseCaught = false;
            client1.MessageReceived += (c, m) => responseCaught = true;
            client2.MessageReceived += async (c, m) =>
            {
                TmqMessage rmsg = m.CreateResponse(TwinoResult.Ok);
                rmsg.SetStringContent("Response!");
                await ((TmqClient) c).SendAsync(rmsg);
            };

            TmqMessage msg = new TmqMessage(MessageType.DirectMessage, "client-2");
            msg.PendingResponse = true;
            msg.SetStringContent("Hello, World!");

            TmqMessage response = await client1.Request(msg);

            Assert.NotNull(response);
            Assert.Equal(msg.MessageId, response.MessageId);
            Assert.Equal(enabled, responseCaught);
        }
    }
}