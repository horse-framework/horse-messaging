using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Operators
{
    public class ConnectionOperatorTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("*client*")]
        public async Task GetOnlineClients(string filter)
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start();

            TmqClient client = new TmqClient();
            client.SetClientType("client-test");
            client.SetClientName("client-test");
            await client.ConnectAsync("tmq://localhost:" + port);

            var result = await client.Connections.GetConnectedClients(filter);
            Assert.Equal(TwinoResultCode.Ok, result.Result.Code);
            Assert.NotNull(result.Model);
            var c = result.Model.FirstOrDefault();
            Assert.NotNull(c);
            Assert.Equal("client-test", c.Type);
        }
    }
}