namespace Horse.Messaging.Data;

/// <summary>
/// When an error is occurred, database object's OnError event will be triggered.
/// This enum has hints to tell end-user what kind of error is occurred
/// </summary>
public enum ErrorHint
{
    /// <summary>
    /// Error occurred while inserting new message into database
    /// </summary>
    Insert = 101,

    /// <summary>
    /// Error occurred while deleting message from database
    /// </summary>
    Delete = 102,

    /// <summary>
    /// Error occurred when another error has occurred and reverse was being attempted
    /// </summary>
    ReverseFromBackup = 111,

    /// <summary>
    /// Error occurred while backup database file is being created
    /// </summary>
    Backup = 112,

    /// <summary>
    /// Error occurred while trying to delete database file
    /// </summary>
    DeleteDatabaseFile = 113,

    /// <summary>
    /// Error occurred on first shrink right after loading database
    /// </summary>
    ShrinkAfterLoad = 121,

    /// <summary>
    /// Error occurred at database shrink operation 
    /// </summary>
    Shrink = 122,

    /// <summary>
    /// Error occurred while syncing shrunken file with database file
    /// </summary>
    SyncShrinkFiles = 123
}