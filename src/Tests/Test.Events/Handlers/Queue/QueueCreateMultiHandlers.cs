using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Events.Annotations;
using Horse.Messaging.Protocol.Events;

namespace Test.Events.Handlers.Queue;

[HorseEvent(HorseEventType.QueueCreate, "multi-queue")]
public class QueueCreateTargetedHandler : IHorseEventHandler
{
    private static int _count;
    public static int Count => Volatile.Read(ref _count);

    public Task Handle(HorseEvent horseEvent, HorseClient client)
    {
        Interlocked.Increment(ref _count);
        return Task.CompletedTask;
    }
}

[HorseEvent(HorseEventType.QueueCreate)]
public class QueueCreateAllTargetsHandler : IHorseEventHandler
{
    private static int _count;
    public static int Count => Volatile.Read(ref _count);

    public Task Handle(HorseEvent horseEvent, HorseClient client)
    {
        Interlocked.Increment(ref _count);
        return Task.CompletedTask;
    }
}
