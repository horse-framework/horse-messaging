using Horse.Messaging.Server;
using Horse.Messaging.Server.Channels;
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
    .ConfigureChannels(cfg => cfg.Options.AutoDestroy = false)
    .Build();
server.UseRider(rider);
server.UseHorseOverWebsockets(cfg =>
{
    cfg.Port = 8080;
    cfg.CertificateKey = "";
    cfg.SslCertificate = @"C:\Projects\github\c.cer";
    cfg.SslEnabled = false;
});

Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(1000);
        foreach (HorseChannel channel in rider.Channel.Channels)
            Console.WriteLine(channel.Name + " : " + channel.ClientsCount() + " clients");
    }
});

server.Run();