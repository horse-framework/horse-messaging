using System.Collections.Generic;

namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Horse event model
    /// </summary>
    public class HorseEvent
    {
        /// <summary>
        /// Event type
        /// </summary>
        public HorseEventType Type { get; set; }

        /// <summary>
        /// Event subject client
        /// </summary>
        public EventSubject Subject { get; set; }

        /// <summary>
        /// Event target
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Event parameters
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Parameters { get; set; }
    }
}