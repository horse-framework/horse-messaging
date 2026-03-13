using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Server;

namespace Test.Websocket;

public class WebSocketTestServer
{
    public HorseRider Rider { get; private set; }
    public HorseServer Server { get; private set; }
    public int HorsePort { get; private set; }
    public int WebSocketPort { get; private set; }

    public async Task Initialize()
    {
        Rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.AutoQueueCreation = true;
            })
            .ConfigureChannels(c => { c.UseCustomPersistentConfigurator(null); })
            .Build();
    }

    public (int horsePort, int wsPort) Start(int pingInterval = 30, int requestTimeout = 30)
    {
        Random rnd = new Random();

        for (int i = 0; i < 50; i++)
        {
            try
            {
                int horsePort = rnd.Next(5000, 65000);
                int wsPort = rnd.Next(5000, 65000);

                while (wsPort == horsePort)
                    wsPort = rnd.Next(5000, 65000);

                HorseServerOptions serverOptions = HorseServerOptions.CreateDefault();
                serverOptions.Hosts[0].Port = horsePort;
                serverOptions.PingInterval = pingInterval;
                serverOptions.RequestTimeout = requestTimeout;

                Server = new HorseServer(serverOptions);
                Server.UseRider(Rider);
                Server.UseHorseOverWebsockets(cfg =>
                {
                    cfg.Port = wsPort;
                    cfg.SslEnabled = false;
                });

                Server.StartAsync().GetAwaiter().GetResult();

                HorsePort = horsePort;
                WebSocketPort = wsPort;

                return (horsePort, wsPort);
            }
            catch
            {
                Thread.Sleep(2);
            }
        }

        return (0, 0);
    }

    public void Stop()
    {
        Server?.StopAsync().GetAwaiter().GetResult();
    }
}
