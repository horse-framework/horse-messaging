namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Route binding types
    /// </summary>
    public enum BindingType
    {
        /// <summary>
        /// Direct message binding.
        /// Messages are sent to direct receivers without queue operation.
        /// </summary>
        Direct = 0,

        /// <summary>
        /// Messages are pushed into queue
        /// </summary>
        Queue = 1,

        /// <summary>
        /// Messages are sent to an HTTP endpoint as JSON request
        /// </summary>
        Http = 2,

        /// <summary>
        /// Messages are pushed to queues with specified tag name
        /// </summary>
        Topic = 3
    }
}