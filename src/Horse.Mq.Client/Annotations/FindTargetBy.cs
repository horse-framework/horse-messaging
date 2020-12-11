namespace Horse.Mq.Client.Annotations
{
    /// <summary>
    /// Used for direct receiver attribute.
    /// Describes how the target will be found.
    /// </summary>
    public enum FindTargetBy
    {
        /// <summary>
        /// Finds client by Id
        /// </summary>
        Id,

        /// <summary>
        /// Finds client by type
        /// </summary>
        Type,

        /// <summary>
        /// Finds client by name
        /// </summary>
        Name
    }
}