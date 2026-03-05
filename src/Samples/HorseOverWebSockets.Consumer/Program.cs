using System.Threading;

IHost host = Host.CreateDefaultBuilder(args)
    .Build();

host.Run();