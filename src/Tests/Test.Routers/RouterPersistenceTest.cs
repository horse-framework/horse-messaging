using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

/// <summary>
/// Binding type defined outside of the server assembly
/// </summary>
public class TestCustomBinding : Binding
{
    public override Task<bool> Send(MessagingClient sender, HorseMessage message) => Task.FromResult(true);
}

public class RouterPersistenceTest
{
    /// <summary>
    /// Delegates to a router file, counts saves and can fail them
    /// </summary>
    private sealed class TrackingConfigurator : IOptionsConfigurator<RouterConfiguration>
    {
        private readonly RouterOptionsConfigurator _inner;
        private int _saveCount;

        public int SaveCount => Volatile.Read(ref _saveCount);
        public bool FailSave { get; set; }

        public TrackingConfigurator(HorseRider rider, string file)
        {
            _inner = new RouterOptionsConfigurator(rider, file);
        }

        public RouterConfiguration[] Load() => _inner.Load();

        public void Save()
        {
            Interlocked.Increment(ref _saveCount);
            if (FailSave)
                throw new IOException("Save failed in test");

            _inner.Save();
        }

        public void Add(RouterConfiguration item) => _inner.Add(item);
        public RouterConfiguration Find(Func<RouterConfiguration, bool> predicate) => _inner.Find(predicate);
        public void Remove(Func<RouterConfiguration, bool> predicate) => _inner.Remove(predicate);
        public void Remove(RouterConfiguration item) => _inner.Remove(item);
        public void Clear() => _inner.Clear();
    }

    internal static string UseNewFile(TestHorseRider server)
    {
        string file = $"routers-{Guid.NewGuid():N}.json";
        RouterOptionsConfigurator configurator = new RouterOptionsConfigurator(server.Rider, file);
        configurator.Load();
        server.Rider.Router.OptionsConfigurator = configurator;
        return file;
    }

    /// <summary>
    /// Builds a new rider on the same data path, as the server does when it starts again
    /// </summary>
    private static HorseRider Restart(TestHorseRider server, Func<HorseRider, IOptionsConfigurator<RouterConfiguration>> configurator)
    {
        string dataPath = server.Rider.Options.DataPath;
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureRouters(c => c.UseCustomPersistentConfigurator(configurator(c.Rider)))
            .Build();
    }

    internal static HorseRider Restart(TestHorseRider server, string file)
    {
        return Restart(server, rider => new RouterOptionsConfigurator(rider, file));
    }

    internal static RouterConfiguration[] ReadSaved(TestHorseRider server, string file)
    {
        return new RouterOptionsConfigurator(server.Rider, file).Load();
    }

    internal static QueueBinding NewQueueBinding(string name)
    {
        return new QueueBinding { Name = name, Target = "push-a", Interaction = BindingInteraction.None };
    }

    private static RouterConfiguration Config(string routerName, params BindingConfiguration[] bindings)
    {
        RouterConfiguration configuration = new RouterConfiguration { Name = routerName, Method = "distribute", IsEnabled = true };
        configuration.Bindings.AddRange(bindings);
        return configuration;
    }

    private static BindingConfiguration BindingConfig(string name, string type, string assemblyName)
    {
        return new BindingConfiguration
        {
            Name = name, Type = type, AssemblyName = assemblyName, Target = "push-a", Interaction = "none", Method = "distribute"
        };
    }

    private static string WriteFile(TestHorseRider server, params RouterConfiguration[] configurations)
    {
        string file = $"routers-{Guid.NewGuid():N}.json";
        RouterOptionsConfigurator writer = new RouterOptionsConfigurator(server.Rider, file);
        writer.Load();
        foreach (RouterConfiguration configuration in configurations)
            writer.Add(configuration);

        writer.Save();
        return file;
    }

    [Fact]
    public async Task RemovedBinding_IsNotLoadedAfterRestart()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            Router router = server.Rider.Router.Add("remove-router", RouteMethod.Distribute);
            QueueBinding removed = NewQueueBinding("removed");
            router.AddBinding(NewQueueBinding("kept"));
            router.AddBinding(removed);

            // the overload the remove request of a client uses
            router.RemoveBinding(removed);

            HorseRider restarted = Restart(server, file);
            Router loaded = restarted.Router.Find("remove-router");
            Assert.NotNull(loaded);
            Assert.Equal(new[] { "kept" }, loaded.Bindings.Select(x => x.Name));
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task BindingContentType_IsRestoredAfterRestart()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            Router router = server.Rider.Router.Add("content-router", RouteMethod.Distribute);
            router.AddBinding(new HttpBinding { Name = "http", Target = "http://localhost:1/", ContentType = (ushort)HttpBindingMethod.Put, Interaction = BindingInteraction.None });
            router.AddBinding(new DirectBinding { Name = "direct", Target = "client-1", ContentType = 1234, Interaction = BindingInteraction.None });

            HorseRider restarted = Restart(server, file);
            Router loaded = restarted.Router.Find("content-router");
            Assert.Equal((ushort)HttpBindingMethod.Put, loaded.Bindings.OfType<HttpBinding>().Single().ContentType);
            Assert.Equal((ushort)1234, loaded.Bindings.OfType<DirectBinding>().Single().ContentType);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RouterCreatedFromConfiguration_IsSavedOnceAndRemovedCompletely()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);

            // the steps of a create message from the main node
            Router replica = server.Rider.Router.CreateRouter(Config("replica-router", BindingConfiguration.Create(NewQueueBinding("q"))));
            server.Rider.Router.Add(replica, false);
            Assert.Single(ReadSaved(server, file), x => x.Name == "replica-router");

            // a router built in code before it is added
            Router coded = new Router(server.Rider, "coded-router", RouteMethod.Distribute);
            coded.AddBinding(NewQueueBinding("q1"));
            coded.AddBinding(NewQueueBinding("q2"));
            server.Rider.Router.Add(coded);

            RouterConfiguration saved = Assert.Single(ReadSaved(server, file), x => x.Name == "coded-router");
            Assert.Equal(2, saved.Bindings.Count);

            server.Rider.Router.Remove(replica);
            Assert.DoesNotContain(ReadSaved(server, file), x => x.Name == "replica-router");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RouterNotAddedToServer_IsNotSaved()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            Router router = server.Rider.Router.Add("existing-router", RouteMethod.Distribute);
            router.AddBinding(NewQueueBinding("q"));

            Router never = new Router(server.Rider, "never-added", RouteMethod.Distribute);
            never.AddBinding(NewQueueBinding("q"));

            // same name as an added router: its bindings must not replace the saved ones
            Router clash = new Router(server.Rider, "existing-router", RouteMethod.Distribute);
            clash.AddBinding(NewQueueBinding("other"));

            RouterConfiguration[] saved = ReadSaved(server, file);
            Assert.DoesNotContain(saved, x => x.Name == "never-added");
            RouterConfiguration existing = Assert.Single(saved, x => x.Name == "existing-router");
            Assert.Equal(new[] { "q" }, existing.Bindings.Select(x => x.Name));
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DuplicateEntries_LastIsLoadedAndFileIsCleaned()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string type = typeof(QueueBinding).FullName;
            string file = WriteFile(server,
                Config("dup-router", BindingConfig("old", type, null)),
                Config("dup-router", BindingConfig("new", type, null)));

            HorseRider restarted = Restart(server, file);
            Assert.Equal(new[] { "new" }, restarted.Router.Find("dup-router").Bindings.Select(x => x.Name));

            RouterConfiguration saved = Assert.Single(ReadSaved(server, file), x => x.Name == "dup-router");
            Assert.Equal("new", saved.Bindings.Single().Name);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Restart_DoesNotSaveRouters()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            for (int i = 0; i < 3; i++)
            {
                Router router = server.Rider.Router.Add($"load-{i}", RouteMethod.Distribute);
                router.AddBinding(NewQueueBinding("a"));
                router.AddBinding(NewQueueBinding("b"));
            }

            TrackingConfigurator tracking = null;
            HorseRider restarted = Restart(server, rider => tracking = new TrackingConfigurator(rider, file));

            Assert.Equal(3, restarted.Router.Routers.Count());
            Assert.Equal(0, tracking.SaveCount);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CustomBindingType_IsRestoredAfterRestart()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            Router router = server.Rider.Router.Add("custom-router", RouteMethod.Distribute);
            router.AddBinding(new TestCustomBinding { Name = "custom", Target = "x", Interaction = BindingInteraction.None });

            HorseRider restarted = Restart(server, file);
            Assert.IsType<TestCustomBinding>(Assert.Single(restarted.Router.Find("custom-router").Bindings));

            // saved by an older version: the type name without its assembly
            string legacyFile = WriteFile(server, Config("legacy-router", BindingConfig("custom", typeof(TestCustomBinding).FullName, null)));
            HorseRider legacy = Restart(server, legacyFile);
            Assert.IsType<TestCustomBinding>(Assert.Single(legacy.Router.Find("legacy-router").Bindings));
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task UnresolvableBinding_IsKeptInConfigurationAndCanBeRemoved()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = WriteFile(server, Config("partial-router",
                BindingConfig("missing", "Missing.Namespace.MissingBinding", "Missing.Assembly"),
                BindingConfig("not-binding", typeof(StringBuilder).FullName, typeof(StringBuilder).Assembly.GetName().Name),
                BindingConfig("q", typeof(QueueBinding).FullName, null)));

            HorseRider restarted = Restart(server, file);
            Router loaded = restarted.Router.Find("partial-router");
            Assert.Equal(new[] { "q" }, loaded.Bindings.Select(x => x.Name));

            // a later save keeps the bindings that could not be loaded
            Assert.True(loaded.AddBinding(NewQueueBinding("q2")));
            Assert.Equal(new[] { "missing", "not-binding", "q", "q2" },
                ReadSaved(server, file).Single(x => x.Name == "partial-router").Bindings.Select(x => x.Name).OrderBy(x => x));

            // their names are taken
            Assert.False(loaded.AddBinding(NewQueueBinding("missing")));

            loaded.RemoveBinding("missing");
            Assert.Equal(new[] { "not-binding", "q", "q2" },
                ReadSaved(server, file).Single(x => x.Name == "partial-router").Bindings.Select(x => x.Name).OrderBy(x => x));
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ConcurrentChanges_AreAllSaved()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = UseNewFile(server);
            Router[] routers = Enumerable.Range(0, 20)
                .Select(i => server.Rider.Router.Add($"parallel-{i}", RouteMethod.Distribute))
                .ToArray();

            // routers change at the same time, the bindings of one router one by one
            bool[][] results = await Task.WhenAll(routers.Select(router => Task.Run(() =>
                Enumerable.Range(0, 5).Select(i => router.AddBinding(NewQueueBinding($"b-{i}"))).ToArray())));

            Assert.All(results.SelectMany(x => x), Assert.True);

            RouterConfiguration[] saved = ReadSaved(server, file);
            foreach (Router router in routers)
                Assert.Equal(5, Assert.Single(saved, x => x.Name == router.Name).Bindings.Count);

            Assert.False(File.Exists($"{server.Rider.Options.DataPath}/{file}.tmp"));
        });
    }

    [Fact]
    public async Task FailedSave_DoesNotUndoChanges()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = $"routers-{Guid.NewGuid():N}.json";
            TrackingConfigurator tracking = new TrackingConfigurator(server.Rider, file);
            tracking.Load();
            tracking.FailSave = true;
            server.Rider.Router.OptionsConfigurator = tracking;

            Router router = server.Rider.Router.Add("failing-save", RouteMethod.Distribute);
            Assert.Same(router, server.Rider.Router.Find("failing-save"));
            Assert.True(router.AddBinding(NewQueueBinding("q")));
            Assert.Single(router.Bindings);

            // the next successful save writes the changes of the failed ones
            tracking.FailSave = false;
            server.Rider.Router.Add("after-failure", RouteMethod.Distribute);

            RouterConfiguration[] saved = ReadSaved(server, file);
            Assert.Single(Assert.Single(saved, x => x.Name == "failing-save").Bindings);
            Assert.Single(saved, x => x.Name == "after-failure");
            await Task.CompletedTask;
        });
    }
}
