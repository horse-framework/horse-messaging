using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Events.Annotations;
using Horse.Messaging.Protocol.Events;

namespace Test.Events.Handlers.Queue
{
    [HorseEvent(HorseEventType.QueueRemove)]
    public class QueueRemoveHandler : IHorseEventHandler
    {
        public static int Count { get; private set; }

        public Task Handle(HorseEvent horseEvent, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}