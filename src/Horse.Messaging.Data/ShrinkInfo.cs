using System;

namespace Horse.Messaging.Data
{
    /// <summary>
    /// Shrink operation info and statistics
    /// </summary>
    public class ShrinkInfo
    {
        /// <summary>
        /// If true, shrink is completed successfuly
        /// </summary>
        public bool Successful { get; set; }

        /// <summary>
        /// Shrink preparation duration (open files, reading current db)
        /// </summary>
        public TimeSpan PreparationDuration { get; set; }

        /// <summary>
        /// Shrink truncate operation duration (reading all messages and removing deleted messages)
        /// </summary>
        public TimeSpan TruncateDuration { get; set; }

        /// <summary>
        /// Shrink sync duration (sync shrink file with current database file and create backup)
        /// </summary>
        public TimeSpan SyncDuration { get; set; }

        /// <summary>
        /// Total duration including all operations
        /// </summary>
        public TimeSpan TotalDuration => PreparationDuration + TruncateDuration + SyncDuration;

        /// <summary>
        /// Total data size that was shrunken
        /// </summary>
        public long OldSize { get; set; }

        /// <summary>
        /// New data size after shrink
        /// </summary>
        public long NewSize { get; set; }

        /// <summary>
        /// Current database size right after shrink is completed
        /// </summary>
        public long CurrentDatabaseSize { get; set; }

        /// <summary>
        /// If an error occured, exception object of the error
        /// </summary>
        public Exception Error { get; set; }
    }
}