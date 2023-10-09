using Horse.Jockey;
using Horse.Jockey.Models.User;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace HorseService;

public class ServiceBuilder
{
    private readonly HorseServer _server = new HorseServer();
    private HorseRider _rider;
    private AppOptions _options;

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
                c.Options.AutoDestroy = true;
                c.Options.AutoChannelCreation = true;
            })
            .ConfigureCache(c =>
            {
                c.Options.DefaultDuration = TimeSpan.FromMinutes(15);
                c.Options.MinimumDuration = TimeSpan.FromHours(6);
            })
            .ConfigureQueues(c =>
            {
                c.Options.AutoQueueCreation = true;
                c.UsePersistentQueues(d => { d.UseAutoFlush(TimeSpan.FromMilliseconds(50)); },
                    q =>
                    {
                        q.Options.AutoDestroy = QueueDestroy.Disabled;
                        q.Options.CommitWhen = CommitWhen.AfterReceived;
                        q.Options.PutBack = PutBackDecision.Regular;
                        q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                        q.Options.PutBackDelay = 5000;
                    });
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

    public HorseServer CreateServer()
    {
        _server.Options.Hosts = new List<HostOptions>();
        _server.Options.Hosts.Add(new HostOptions {Port = _options.Port});

        _server.UseRider(_rider);
        _server.Logger = new ConsoleLogger();

        return _server;
    }
}