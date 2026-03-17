using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Jockey;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Benchmark.Server;

class Program
{
    private static HorseRider _rider;

    static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("[P]ersistent or [M]emory queues?");
        string persistence = Console.ReadLine().ToUpper();

        Console.Write("[W]ait for acknowledge, [J]ust request or [N]one?");
        string acks = Console.ReadLine().ToUpper();

        Console.Write("Producer commit when? After [R]Saved, After [S]ent or After [A]cknowledged?");
        string commit = Console.ReadLine().ToUpper();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Press enter when you are ready");
        Console.ReadLine();

        _rider = HorseRiderBuilder.Create()
            .ConfigureChannels(cfg =>
            {
                cfg.Options.AutoChannelCreation = true;
                cfg.Options.AutoDestroy = false;
                cfg.Options.SendLastMessageAsInitial = true;
            })
            .ConfigureQueues(cfg =>
            {
                cfg.EventHandlers.Add(new QueueEventHandler());

                cfg.Options.Type = QueueType.RoundRobin;
                cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);

                if (acks == "W")
                    cfg.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                else if (acks == "J")
                    cfg.Options.Acknowledge = QueueAckDecision.JustRequest;
                else
                    cfg.Options.Acknowledge = QueueAckDecision.None;

                if (commit == "R")
                    cfg.Options.CommitWhen = CommitWhen.AfterReceived;
                else if (commit == "S")
                    cfg.Options.CommitWhen = CommitWhen.AfterSent;
                else if (commit == "A")
                    cfg.Options.CommitWhen = CommitWhen.AfterAcknowledge;

                if (persistence == "P")
                {
                    cfg.UsePersistentQueues(c => { c.UseAutoFlush(TimeSpan.FromMilliseconds(250)); });
                }
                else
                {
                    cfg.UseMemoryQueues();
                }
            })
            .AddJockey(cfg => { cfg.Port = 2627; })
            .ConfigureCache(cfg => { cfg.Options.DefaultDuration = TimeSpan.FromMinutes(30); })
            .AddErrorHandler<ErrorHandler>()
            .Build();

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 2626 }];

        server.UseRider(_rider);
        server.UseHorseOverWebsockets(o => o.Port = 2680);
        
        await server.RunAsync(new CancellationTokenSource().Token);
    }
}