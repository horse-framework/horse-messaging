namespace Horse.Mq.Data
{
    /// <summary>
    /// Data types for message objects that are saved to disk 
    /// </summary>
    public enum DataType : byte
    {
        /// <summary>
        /// Empty message.
        /// This message is deleted but the data still remain.
        /// That means, the allocated content will be removed in next shrink.
        /// </summary>
        Empty = 0x00,

        /// <summary>
        /// Inserted message data.
        /// This type tells the data is stored and will be.
        /// </summary>
        Insert = 0x10,

        /// <summary>
        /// A delete record for a data.
        /// In next shrink, data itself will be found by that delete data and it will be removed.
        /// </summary>
        Delete = 0x11
    }
}