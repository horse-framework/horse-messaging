using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Events.Annotations;
using Horse.Messaging.Protocol.Events;

namespace Test.Client.Handlers
{
    [HorseEvent(HorseEventType.QueueCreate)]
    public class EventHandler : IHorseEventHandler
    {
        public Task Handle(HorseEvent horseEvent, HorseClient client)
        {
            return Task.CompletedTask;
        }
    }
}