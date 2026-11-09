using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Server;
using Xunit;

namespace Test.Nodes;

/// <summary>
/// Direct messages and responses in a Scaled cluster: a client message is relayed to every node,
/// each node delivers it to its own clients.
/// </summary>
public class ScaledDirectMessageTest
{
    private sealed class ScaledNode
    {
        public HorseRider Rider { get; init; }
        public HorseServer Server { get; init; }
        public int Port { get; init; }
    }

    private static int FreePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<List<ScaledNode>> StartScaledCluster(int count)
    {
        int[] ports = Enumerable.Range(0, count).Select(_ => FreePort()).ToArray();
        List<ScaledNode> nodes = new();

        for (int i = 0; i < count; i++)
        {
            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureQueues(q =>
                {
                    q.UseMemoryQueues();
                    q.UseCustomPersistentConfigurator(null);
                })
                .ConfigureChannels(c => c.UseCustomPersistentConfigurator(null))
                .Build();

            rider.Cluster.Options.Name = $"Node-{ports[i]}";
            rider.Cluster.Options.SharedSecret = "scaled-secret";
            rider.Cluster.Options.NodeHost = $"horse://localhost:{ports[i]}";
            rider.Cluster.Options.PublicHost = $"horse://localhost:{ports[i]}";
            rider.Cluster.Options.Mode = ClusterMode.Scaled;

            for (int j = 0; j < count; j++)
            {
                if (j == i)
                    continue;

                rider.Cluster.Options.Nodes.Add(new NodeInfo
                {
                    Name = $"Node-{ports[j]}",
                    Host = $"horse://localhost:{ports[j]}",
                    PublicHost = $"horse://localhost:{ports[j]}"
                });
            }

            HorseServer server = new HorseServer();
            server.Options.Hosts = [new HorseHostOptions { Port = ports[i] }];
            server.UseRider(rider);
            _ = server.StartAsync();

            nodes.Add(new ScaledNode { Rider = rider, Server = server, Port = ports[i] });
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && !nodes.All(n => n.Rider.Cluster.Clients.All(c => c.IsConnected)))
            await Task.Delay(100);

        Assert.True(nodes.All(n => n.Rider.Cluster.Clients.All(c => c.IsConnected)), "cluster nodes could not connect");
        await Task.Delay(500);
        return nodes;
    }

    private static async Task StopCluster(IEnumerable<ScaledNode> nodes)
    {
        foreach (ScaledNode node in nodes)
        {
            try
            {
                await node.Server.StopAsync();
            }
            catch
            {
            }
        }
    }

    private static async Task<HorseClient> Connect(ScaledNode node, string id, string name = null)
    {
        HorseClient client = new HorseClient();
        client.ClientId = id;
        client.ResponseTimeout = TimeSpan.FromSeconds(3);

        if (name != null)
            client.SetClientName(name);

        await client.ConnectAsync("horse://localhost:" + node.Port);
        Assert.True(client.IsConnected);
        return client;
    }

    /// <summary>
    /// Answers requests and records the source of the last direct message
    /// </summary>
    private static Func<string> Responder(HorseClient client)
    {
        string source = null;
        client.MessageReceived += async (_, m) =>
        {
            if (m.Type != MessageType.DirectMessage)
                return;

            Volatile.Write(ref source, m.Source);
            if (m.WaitResponse)
                await client.SendAsync(m.CreateResponse(HorseResultCode.Ok), CancellationToken.None);
        };

        return () => Volatile.Read(ref source);
    }

    private static HorseMessage Direct(string target)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target);
        message.SetStringContent("hello");
        return message;
    }

    [Fact]
    public async Task RequestToClientOnSameNode_GetsResponse_AndSourceIsSender()
    {
        List<ScaledNode> nodes = await StartScaledCluster(2);
        try
        {
            HorseClient sender = await Connect(nodes[0], "sender");
            Func<string> source = Responder(await Connect(nodes[0], "receiver"));

            HorseResult result = await sender.SendAsync(Direct("receiver"), true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.Equal("sender", source());
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    [Fact]
    public async Task RequestToClientOnOtherNode_GetsResponse_AndSourceIsSender()
    {
        List<ScaledNode> nodes = await StartScaledCluster(2);
        try
        {
            HorseClient sender = await Connect(nodes[0], "sender");
            Func<string> source = Responder(await Connect(nodes[1], "receiver"));

            HorseResult result = await sender.SendAsync(Direct("receiver"), true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.Equal("sender", source());
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    [Fact]
    public async Task NameKnownOnlyByOneNode_DoesNotDropNodeConnection()
    {
        List<ScaledNode> nodes = await StartScaledCluster(2);
        try
        {
            HorseClient outgoing = (HorseClient)typeof(NodeClient)
                .GetField("_outgoingClient", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(nodes[0].Rider.Cluster.Clients[0]);

            int drops = 0;
            outgoing!.Disconnected += _ => Interlocked.Increment(ref drops);

            HorseClient sender = await Connect(nodes[0], "sender");
            await Connect(nodes[0], "named", "only-on-first-node");

            await sender.SendAsync(Direct("@name:only-on-first-node"), CancellationToken.None);
            await sender.SendAsync(Direct("@type:only-on-first-node"), CancellationToken.None);
            await Task.Delay(800);

            Assert.Equal(0, drops);
            Assert.True(sender.IsConnected);
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// The node of the sender cannot know whether another node has the receiver,
    /// so it does not answer NotFound while other nodes are connected. The request times out instead.
    /// </summary>
    [Fact]
    public async Task MissingReceiver_IsNotAnsweredNotFound_WhileOtherNodesAreConnected()
    {
        List<ScaledNode> nodes = await StartScaledCluster(2);
        try
        {
            HorseClient sender = await Connect(nodes[0], "sender");
            sender.ResponseTimeout = TimeSpan.FromSeconds(1);

            HorseResult result = await sender.SendAsync(Direct("missing"), true, CancellationToken.None);

            Assert.Equal(HorseResultCode.RequestTimeout, result.Code);
            Assert.True(sender.IsConnected);
        }
        finally
        {
            await StopCluster(nodes);
        }
    }
}
