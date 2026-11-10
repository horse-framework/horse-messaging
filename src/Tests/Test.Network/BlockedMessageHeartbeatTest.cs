using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;
using Test.Common;
using Xunit;

namespace Test.Network;

/// <summary>
/// A message whose processing waits (a push waiting for a queue lock held by a queue sync, for example)
/// must not stop reading the connection. The server used to read the next message of a connection only
/// after the previous one was processed, so PONGs of the client stayed unread and the heartbeat
/// disconnected a healthy client.
/// </summary>
public class BlockedMessageHeartbeatTest
{
    private sealed class BlockingAuthorization : IClientAuthorization
    {
        private readonly object _lock = new();

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Order { get; } = new();

        public async Task<bool> CanMessageToQueue(MessagingClient client, HorseQueue queue, HorseMessage message)
        {
            lock (_lock)
                Order.Add(message.GetStringContent());

            Entered.TrySetResult();
            await Gate.Task;
            return true;
        }

        public Task<bool> CanDirectMessage(MessagingClient sender, HorseMessage message, MessagingClient receiver) => Task.FromResult(true);
        public Task<bool> CanCreateQueue(MessagingClient client, string name, NetworkOptionsBuilder options) => Task.FromResult(true);
        public Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue) => Task.FromResult(true);
        public bool CanSubscribeEvent(MessagingClient client, HorseEventType eventType, string target) => true;
        public Task<bool> CanCreateRouter(MessagingClient client, string routerName, RouteMethod method) => Task.FromResult(true);
        public Task<bool> CanRemoveRouter(MessagingClient client, Router router) => Task.FromResult(true);
        public Task<bool> CanCreateBinding(MessagingClient client, Router router, BindingInformation binding) => Task.FromResult(true);
        public Task<bool> CanRemoveBinding(MessagingClient client, Binding binding) => Task.FromResult(true);
    }

    [Fact]
    public async Task BlockedPush_PongOfSameConnection_IsStillRead_AndMessagesKeepOrder()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            BlockingAuthorization authorization = new BlockingAuthorization();
            server.Rider.Client.Authorizations.Add(authorization);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60);
            await client.ConnectAsync($"horse://localhost:{port}");
            await TestHorseRider.WaitUntil(() => server.Rider.Client.Find(client.ClientId) != null, 5000);

            MessagingClient serverClient = server.Rider.Client.Find(client.ClientId);
            Assert.NotNull(serverClient);

            Task<HorseResult> first = client.Queue.Push("push-a", Encoding.UTF8.GetBytes("1"), true);
            Assert.True(await Task.WhenAny(authorization.Entered.Task, Task.Delay(5000)) == authorization.Entered.Task);

            Task<HorseResult> second = client.Queue.Push("push-a", Encoding.UTF8.GetBytes("2"), true);

            //server time is updated periodically, wait until it moves
            long before = serverClient.LastAliveTimeTicks;
            await Task.Delay(2500);
            Assert.True(await client.SendRawAsync(PredefinedMessages.PONG));

            bool alive = false;
            for (int i = 0; i < 30 && !alive; i++)
            {
                await Task.Delay(100);
                alive = serverClient.LastAliveTimeTicks > before;
            }

            Assert.True(alive, "PONG was not read while a previous message of the connection was processed");

            //the second push is read but waits for the first one
            Assert.Equal(new[] { "1" }, authorization.Order.ToArray());

            authorization.Gate.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(new[] { "1", "2" }, authorization.Order.ToArray());
            Assert.True(client.IsConnected);

            client.Disconnect();
        }, pingInterval: 300, requestTimeout: 300);
    }
}
