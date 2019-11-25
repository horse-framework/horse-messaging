namespace Twino.MQ.Options
{
    /// <summary>
    /// Listener and host binding options
    /// </summary>
    public class ListenerOptions
    {
        /// <summary>
        /// Listening port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Allowed methods. null or *, allows all methods
        /// </summary>
        public string[] AllowedMethods { get; set; }

        /// <summary>
        /// Allowed paths. null or *, allows all paths
        /// </summary>
        public string[] AllowedPaths { get; set; }
    }
}