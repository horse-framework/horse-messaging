using System.Threading.Tasks;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Horse event consumer implementation
    /// </summary>
    internal interface IEventConsumer
    {
        /// <summary>
        /// Called when the event is triggered 
        /// </summary>
        Task OnEvent(HorseEvent horseEvent);
    }
}