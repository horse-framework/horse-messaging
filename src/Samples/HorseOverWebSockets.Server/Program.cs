using Horse.Messaging.Server;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Server;
using HostOptions = Horse.Server.HostOptions;

HorseServer server = new HorseServer();
server.Options.Hosts = new List<HostOptions>
{
    new HostOptions {Port = 2626}
};
HorseRider rider = HorseRiderBuilder
    .Create()
    .ConfigureQueues(cfg => cfg.UseMemoryQueues())
    .Build();
server.UseRider(rider);
server.UseHorseOverWebsockets(cfg => cfg.Port = 8080);
server.Run();