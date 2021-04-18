namespace Horse.Messaging.Data
{
    /// <summary>
    /// When an error is occured, database object's OnError event will be triggered.
    /// This enum has hints to tell end-user what kind of error is occured
    /// </summary>
    public enum ErrorHint
    {
        /// <summary>
        /// Error occured while inserting new message into database
        /// </summary>
        Insert = 101,

        /// <summary>
        /// Error occured while deleting message from database
        /// </summary>
        Delete = 102,

        /// <summary>
        /// Error occured when another error has occured and reverse was being attempted
        /// </summary>
        ReverseFromBackup = 111,

        /// <summary>
        /// Error occured while backup database file is being created
        /// </summary>
        Backup = 112,

        /// <summary>
        /// Error occured while trying to delete database file
        /// </summary>
        DeleteDatabaseFile = 113,

        /// <summary>
        /// Error occured on first shrink right after loading database
        /// </summary>
        ShrinkAfterLoad = 121,

        /// <summary>
        /// Error occured at database shrink operation 
        /// </summary>
        Shrink = 122,

        /// <summary>
        /// Error occured while syncing shrunken file with database file
        /// </summary>
        SyncShrinkFiles = 123
    }
}