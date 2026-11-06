using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Channels;
using Test.Common;
using Xunit;

namespace Test.Channels;

public class ChannelPersistenceTest
{
    /// <summary>
    /// In memory configurator, optionally fails on save
    /// </summary>
    private class MemoryConfigurator : IOptionsConfigurator<ChannelConfiguration>
    {
        public List<ChannelConfiguration> Items { get; } = new();
        public bool FailOnSave { get; set; }

        public ChannelConfiguration[] Load()
        {
            lock (Items)
                return Items.ToArray();
        }

        public void Save()
        {
            if (FailOnSave)
                throw new IOException("disk is full");
        }

        public void Add(ChannelConfiguration item)
        {
            lock (Items)
                Items.Add(item);
        }

        public ChannelConfiguration Find(Func<ChannelConfiguration, bool> predicate)
        {
            lock (Items)
                return Items.FirstOrDefault(predicate);
        }

        public void Remove(Func<ChannelConfiguration, bool> predicate)
        {
            lock (Items)
                Items.RemoveAll(x => predicate(x));
        }

        public void Remove(ChannelConfiguration item)
        {
            lock (Items)
                Items.Remove(item);
        }

        public void Clear()
        {
            lock (Items)
                Items.Clear();
        }
    }

    private class CreatedCounter : IChannelEventHandler
    {
        public int Created;
        public int Removed;
        public Task OnCreated(HorseChannel channel) => Task.FromResult(Interlocked.Increment(ref Created));
        public Task OnRemoved(HorseChannel channel) => Task.FromResult(Interlocked.Increment(ref Removed));
        public Task OnPublish(HorseChannel channel, HorseMessage message) => Task.CompletedTask;
        public Task OnSubscribe(HorseChannel channel, Horse.Messaging.Server.Clients.MessagingClient client) => Task.CompletedTask;
        public Task OnUnsubscribe(HorseChannel channel, Horse.Messaging.Server.Clients.MessagingClient client) => Task.CompletedTask;
    }

    /// <summary>
    /// Changes the channel synchronously in OnCreated
    /// </summary>
    private class OptionsChanger : IChannelEventHandler
    {
        public Task OnCreated(HorseChannel channel)
        {
            channel.Topic = "news";
            channel.Status = ChannelStatus.Paused;
            channel.Options.ClientLimit = 7;
            return Task.CompletedTask;
        }

        public Task OnRemoved(HorseChannel channel) => Task.CompletedTask;
        public Task OnPublish(HorseChannel channel, HorseMessage message) => Task.CompletedTask;
        public Task OnSubscribe(HorseChannel channel, Horse.Messaging.Server.Clients.MessagingClient client) => Task.CompletedTask;
        public Task OnUnsubscribe(HorseChannel channel, Horse.Messaging.Server.Clients.MessagingClient client) => Task.CompletedTask;
    }

    private static HorseRider BuildRider(IOptionsConfigurator<ChannelConfiguration> configurator)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = $"data-{Environment.TickCount}-{Random.Shared.Next(0, 10000)}")
            .ConfigureChannels(c => c.UseCustomPersistentConfigurator(configurator))
            .Build();
    }

    [Fact]
    public async Task FailedSaveRollsBackChannelCreation()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            MemoryConfigurator configurator = new MemoryConfigurator { FailOnSave = true };
            server.Rider.Channel.OptionsConfigurator = configurator;
            CreatedCounter counter = new CreatedCounter();
            server.Rider.Channel.EventHandlers.Add(counter);

            await Assert.ThrowsAsync<IOException>(() => server.Rider.Channel.Create("not-saved"));

            Assert.Null(server.Rider.Channel.Find("not-saved"));
            Assert.Empty(configurator.Items);

            //from client, auto creation by subscribe fails the same way
            ChannelTestClient client = await ChannelTestClient.Connect(port);
            HorseResult subscribe = await client.Client.Channel.Subscribe("not-saved-auto", true, CancellationToken.None);
            Assert.NotEqual(HorseResultCode.Ok, subscribe.Code);
            Assert.Null(server.Rider.Channel.Find("not-saved-auto"));
            Assert.Empty(configurator.Items);

            //the configuration is saved after OnCreated, a channel that cannot be saved is removed again
            await Task.Delay(100);
            Assert.Equal(2, counter.Created);
            Assert.Equal(2, counter.Removed);

            //after the disk recovers, the same name can be created
            configurator.FailOnSave = false;
            Assert.NotNull(await server.Rider.Channel.Create("not-saved"));
            Assert.Single(configurator.Items);
            Assert.Equal(3, counter.Created);
            Assert.Equal(2, counter.Removed);
        });
    }

    [Fact]
    public async Task ChangesMadeInOnCreatedAreSaved()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            MemoryConfigurator configurator = new MemoryConfigurator();
            server.Rider.Channel.OptionsConfigurator = configurator;
            server.Rider.Channel.EventHandlers.Add(new OptionsChanger());

            HorseChannel channel = await server.Rider.Channel.Create("changed-on-created");
            Assert.Equal("news", channel.Topic);

            ChannelConfiguration saved = configurator.Find(x => x.Name == "changed-on-created");
            Assert.NotNull(saved);
            Assert.Equal("news", saved.Topic);
            Assert.Equal("paused", saved.Status);
            Assert.Equal(7, saved.ClientLimit);
        });
    }

    [Fact]
    public async Task ConcurrentChangesAndSavesDoNotFail()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            ChannelOptionsConfigurator configurator = new ChannelOptionsConfigurator(server.Rider, $"channels-{Guid.NewGuid()}.json");
            configurator.Load();

            Task[] workers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    string name = $"w{worker}-c{i}";
                    configurator.Add(new ChannelConfiguration { Name = name, Status = "running" });
                    configurator.Save();

                    if (i % 2 == 0)
                    {
                        configurator.Remove(x => x.Name == name);
                        configurator.Save();
                    }
                }
            })).ToArray();

            //throws "Collection was modified" or IOException without the lock
            await Task.WhenAll(workers);

            ChannelConfiguration[] loaded = configurator.Load();
            Assert.Equal(8 * 100, loaded.Length);
        });
    }

    [Fact]
    public void MissingStatusIsLoadedAsRunning()
    {
        MemoryConfigurator configurator = new MemoryConfigurator();
        configurator.Items.Add(new ChannelConfiguration { Name = "no-status", Status = null });
        configurator.Items.Add(new ChannelConfiguration { Name = "paused-one", Status = "paused", Topic = "news" });
        configurator.Items.Add(new ChannelConfiguration { Name = "destroyed-one", Status = "destroyed" });

        HorseRider rider = BuildRider(configurator);

        HorseChannel noStatus = rider.Channel.Find("no-status");
        Assert.NotNull(noStatus);
        Assert.Equal(ChannelStatus.Running, noStatus.Status);

        HorseChannel paused = rider.Channel.Find("paused-one");
        Assert.Equal(ChannelStatus.Paused, paused.Status);
        Assert.Equal("news", paused.Topic);
        Assert.Equal("news", paused.Info.Topic);
        Assert.Equal(ChannelStatus.Paused.ToString(), paused.Info.Status);

        Assert.Null(rider.Channel.Find("destroyed-one"));
    }

    private static HorseRider BuildRider(string dataPath)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .Build();
    }

    [Fact]
    public async Task SavedOptionsAreRestoredAfterRestart()
    {
        string dataPath = $"data-{Environment.TickCount}-{Random.Shared.Next(0, 10000)}";

        HorseRider first = BuildRider(dataPath);
        await first.Channel.Create("restored", o =>
        {
            o.SendLastMessageAsInitial = true;
            o.AutoDestroyIdleSeconds = 300;
            o.AutoDestroy = false;
            o.ClientLimit = 7;
            o.MessageSizeLimit = 2048;
        });

        HorseRider second = BuildRider(dataPath);
        HorseChannel channel = second.Channel.Find("restored");

        Assert.NotNull(channel);
        Assert.True(channel.Options.SendLastMessageAsInitial);
        Assert.Equal(300, channel.Options.AutoDestroyIdleSeconds);
        Assert.False(channel.Options.AutoDestroy);
        Assert.Equal(7, channel.Options.ClientLimit);
        Assert.Equal(2048UL, channel.Options.MessageSizeLimit);
    }

    [Fact]
    public void FileWithoutIdleSecondsKeepsDefaultIdleSeconds()
    {
        string dataPath = $"data-{Environment.TickCount}-{Random.Shared.Next(0, 10000)}";
        Directory.CreateDirectory(dataPath);

        //written by a version that did not save AutoDestroyIdleSeconds
        File.WriteAllText(Path.Combine(dataPath, "channels.json"),
            "[{\"Name\":\"old-format\",\"Status\":\"running\",\"ClientLimit\":0,\"MessageSizeLimit\":0,\"AutoDestroy\":true,\"SendLastMessageAsInitial\":false}]");

        HorseRider rider = BuildRider(dataPath);
        HorseChannel channel = rider.Channel.Find("old-format");

        Assert.NotNull(channel);
        Assert.Equal(new HorseChannelOptions().AutoDestroyIdleSeconds, channel.Options.AutoDestroyIdleSeconds);
    }
}
