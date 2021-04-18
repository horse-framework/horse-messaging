namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// HTTP Methods for HTTP Binding
    /// </summary>
    public enum HttpBindingMethod : ushort
    {
        /// <summary>
        /// "GET"
        /// </summary>
        Get = 0,

        /// <summary>
        /// "POST"
        /// </summary>
        Post = 1,

        /// <summary>
        /// "PUT"
        /// </summary>
        Put = 2,

        /// <summary>
        /// "PATCH"
        /// </summary>
        Patch = 3,

        /// <summary>
        /// "DELETE"
        /// </summary>
        Delete = 4
    }
}