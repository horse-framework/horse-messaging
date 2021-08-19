using System.Threading.Tasks;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Horse event consumer implementation
    /// </summary>
    public interface IHorseEventHandler
    {
        /// <summary>
        /// Called when the event is triggered 
        /// </summary>
        Task Handle(HorseEvent horseEvent, HorseClient client);
    }
}