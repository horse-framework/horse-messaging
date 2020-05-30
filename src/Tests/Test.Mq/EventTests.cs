using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Xunit;

namespace Test.Mq
{
    /// <summary>
    /// Ports 42250 - 42280
    /// </summary>
    public class EventTests
    {
        [Fact]
        public async Task ClientConnected()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42251);
            server.Start(3000, 3000);
            
            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42251");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientConnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);
            
            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:42251");
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientConnected();
            Assert.True(unsubscribed);
            client2.Disconnect();
            await client2.ConnectAsync("tmq://localhost:42251");
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            
            Assert.False(received);
        }

        [Fact]
        public async Task ClientDisconnected()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42252);
            server.Start(3000, 3000);
            
            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42252");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientDisconnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);
            
            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:42252");
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientDisconnected();
            Assert.True(unsubscribed);
            
            await client2.ConnectAsync("tmq://localhost:42252");
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);
            
            Assert.False(received);
        }

        public async Task ClientJoined()
        {
            throw new NotImplementedException();
        }

        public async Task ClientLeft()
        {
            throw new NotImplementedException();
        }

        public async Task ChannelCreated()
        {
            throw new NotImplementedException();
        }

        public async Task ChannelRemoved()
        {
            throw new NotImplementedException();
        }

        public async Task QueueCreated()
        {
            throw new NotImplementedException();
        }

        public async Task QueueUpdated()
        {
            throw new NotImplementedException();
        }

        public async Task QueueRemoved()
        {
            throw new NotImplementedException();
        }
        
        public async Task MessageProduced()
        {
            throw new NotImplementedException();
        }
    }
}