using Horse.Jockey;
using Horse.Jockey.Models.User;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Server;

namespace HorseService;

public class ServiceBuilder
{
    private readonly HorseServer _server = new HorseServer();
    private HorseRider _rider;
    private AppOptions _options;

    private ServiceBuilder()
    {
        _server.Logger = new ConsoleLogger();
    }

    public static ServiceBuilder Create()
    {
        return new ServiceBuilder();
    }

    public ServiceBuilder SetOptions(AppOptions options)
    {
        _options = options;
        return this;
    }

    public ServiceBuilder ConfigureRider()
    {
        if (_options == null)
            throw new InvalidOperationException("Call SetOptions methods before calling ConfigureRider Options");

        _rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => { o.DataPath = _options.DataPath; })
            .ConfigureChannels(c =>
            {
                c.Options.AutoChannelCreation = _options.ChannelAutoCreate;
                c.Options.AutoDestroy = _options.ChannelAutoDestroy;

                if (_options.ChannelAutoDestroy)
                    c.Options.AutoDestroyIdleSeconds = 15;
            })
            .ConfigureCache(c =>
            {
                c.Options.MaximumKeys = _options.CacheMaxKeys;
                c.Options.ValueMaxSize = _options.CacheMaxValueSize;
                c.Options.DefaultDuration = TimeSpan.FromMinutes(15);
                c.Options.MinimumDuration = TimeSpan.Zero;
            })
            .ConfigureQueues(c =>
            {
                c.Options.Acknowledge = _options.QueueAck;
                c.Options.AutoQueueCreation = _options.QueueAutoCreate;
                c.Options.AutoDestroy = _options.QueueDestroy;
                c.Options.CommitWhen = _options.QueueCommitWhen;
                c.Options.MessageTimeout = _options.QueueMessageTimeout;
                c.Options.PutBack = _options.QueuePutback;

                if (_options.QueuePutbackDelay > TimeSpan.Zero)
                    c.Options.PutBackDelay = Convert.ToInt32(_options.QueuePutbackDelay.TotalMilliseconds);

                if (_options.QueueUsePersistentManagerAsDefault)
                {
                    if (_options.QueueUsePersistent)
                        c.UsePersistentQueues("Persistent", d => { d.UseAutoFlush(TimeSpan.FromMilliseconds(250)); });

                    if (_options.QueueUseMemory)
                        c.UseMemoryQueues("Memory");
                }
                else
                {
                    if (_options.QueueUseMemory)
                        c.UseMemoryQueues("Memory");

                    if (_options.QueueUsePersistent)
                        c.UsePersistentQueues("Persistent", d => { d.UseAutoFlush(TimeSpan.FromMilliseconds(250)); });
                }
            })
            .Build();

        return this;
    }

    public ServiceBuilder InitializeClusterOptions()
    {
        if (_options == null)
            throw new InvalidOperationException("Call SetOptions methods before calling Initialize Cluster Options");

        if (_rider == null)
            throw new InvalidOperationException("Call ConfigureRider methods before calling Initialize Cluster Options");

        if (_options.OtherNodes.Count == 0)
            return this;

        _rider.Cluster.Options.Mode = _options.ClusterMode;
        _rider.Cluster.Options.Name = _options.NodeName;
        _rider.Cluster.Options.NodeHost = $"horse://{_options.Host}:{_options.Port}";
        _rider.Cluster.Options.PublicHost = $"horse://{_options.Host}:{_options.Port}";
        _rider.Cluster.Options.SharedSecret = _options.ClusterSecret;
        _rider.Cluster.Options.Acknowledge = _options.ReplicaAcknowledge;

        Console.WriteLine($"Cluster mode activated as {_rider.Cluster.Options.Mode} mode");

        foreach (NodeOptions node in _options.OtherNodes)
        {
            _rider.Cluster.Options.Nodes.Add(new NodeInfo
            {
                Name = node.Name,
                Host = $"{node.Host}:{_options.Port}",
                PublicHost = $"{node.Host}:{_options.Port}"
            });

            Console.WriteLine($"Cluster node [{node.Name}] added with [{node.Host}] address");
        }

        return this;
    }

    public ServiceBuilder InitializeJockey()
    {
        if (_options == null)
            throw new InvalidOperationException("Call SetOptions methods before calling InitializeJockey Options");

        if (!_options.JockeyEnabled)
            return this;

        _rider.AddJockey(o =>
        {
            o.CustomSecret = $"{Guid.NewGuid()}-{Guid.NewGuid()}-{Guid.NewGuid()}";
            o.Port = _options.JockeyPort;
            o.AuthAsync = login =>
            {
                if (login.Username == _options.JockeyUsername && login.Password == _options.JockeyPassword)
                    return Task.FromResult(new UserInfo {Id = "*", Name = _options.JockeyUsername});

                return Task.FromResult<UserInfo>(null);
            };
        });

        return this;
    }

    public HorseServer Build()
    {
        _server.Options.Hosts = new List<HostOptions>();
        _server.Options.Hosts.Add(new HostOptions {Port = _options.Port});

        _server.UseRider(_rider);

        if (_options.OverWebSocket)
            _server.UseHorseOverWebsockets(opt => opt.Port = 2680);

        return _server;
    }
}