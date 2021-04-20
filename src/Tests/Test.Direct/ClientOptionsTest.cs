using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Direct
{
    public class ClientOptionsTest
    {
        /// <summary>
        /// Subscribes message received event of HorseClient.
        /// Sends a message and waits for response.
        /// If catching response is enabled, response message should trigger message received event.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CatchResponseMessages(bool enabled)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client1 = new HorseClient();
            HorseClient client2 = new HorseClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client1.CatchResponseMessages = enabled;

            await client1.ConnectAsync("horse://localhost:" + port);
            await client2.ConnectAsync("horse://localhost:" + port);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool responseCaught = false;
            client1.MessageReceived += (c, m) => responseCaught = true;
            client2.MessageReceived += async (c, m) =>
            {
                HorseMessage rmsg = m.CreateResponse(HorseResultCode.Ok);
                rmsg.SetStringContent("Response!");
                await ((HorseClient) c).SendAsync(rmsg);
            };

            HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "client-2");
            msg.WaitResponse = true;
            msg.SetStringContent("Hello, World!");

            HorseMessage response = await client1.Request(msg);
            await Task.Delay(500);
            Assert.NotNull(response);
            Assert.Equal(msg.MessageId, response.MessageId);
            Assert.Equal(enabled, responseCaught);
        }
    }
}