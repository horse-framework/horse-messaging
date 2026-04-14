using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Test.Common;
using Test.Events.Handlers.Queue;
using Xunit;

namespace Test.Events;

public class QueueEventTest
{
    [Fact]
    public async Task QueueCreate()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueCreateHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Queue.Create("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueCreateHandler.Count);
        });
    }

    [Fact]
    public async Task QueueRemove()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueRemoveHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Queue.Create("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, QueueRemoveHandler.Count);

            HorseResult removeResult = await client.Queue.Remove("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, removeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueRemoveHandler.Count);
        });
    }

    [Fact]
    public async Task QueueSubscribe()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueSubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Queue.Subscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueSubscribeHandler.Count);
        });
    }

    [Fact]
    public async Task QueueUnsubscribe()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueUnsubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Queue.Subscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, QueueUnsubscribeHandler.Count);

            HorseResult unsubscribeResult = await client.Queue.Unsubscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, unsubscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueUnsubscribeHandler.Count);
        });
    }
}
