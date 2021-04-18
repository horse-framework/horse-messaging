namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Event subject model
    /// </summary>
    public class EventSubject
    {
        /// <summary>
        /// Event subject unique id
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Event subject name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Event subject type
        /// </summary>
        public string Type { get; set; }
    }
}