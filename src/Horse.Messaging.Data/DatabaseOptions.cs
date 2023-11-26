using System;

namespace Horse.Messaging.Data;

/// <summary>
/// Database options
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// Database file name. May be relative of absolute full path
    /// </summary>
    public string Filename { get; set; }

    /// <summary>
    /// If true, database file will be shrunken automatically in ShrinkInterval interval
    /// </summary>
    public bool AutoShrink { get; set; }

    /// <summary>
    /// If AutoShrink is enabled, auto shrink interval duration.
    /// </summary>
    public TimeSpan ShrinkInterval { get; set; }

    /// <summary>
    /// If true, backup file will be created after each shrink.
    /// </summary>
    public bool CreateBackupOnShrink { get; set; }

    /// <summary>
    /// If true, database file stream flush will be called right after each insert/delete operation.
    /// This may hurt performance but guarantees flush.
    /// </summary>
    public bool InstantFlush { get; set; }

    /// <summary>
    /// If true, database file will be flushed in a period if it's necessary.
    /// </summary>
    public bool AutoFlush { get; set; }

    /// <summary>
    /// If AutoFlush is true, database file stream flush interval duration.
    /// </summary>
    public TimeSpan FlushInterval { get; set; }

    internal DatabaseOptions Clone()
    {
        return new DatabaseOptions
        {
            Filename = Filename,
            AutoShrink = AutoShrink,
            ShrinkInterval = ShrinkInterval,
            CreateBackupOnShrink = CreateBackupOnShrink,
            InstantFlush = InstantFlush,
            AutoFlush = AutoFlush,
            FlushInterval = FlushInterval
        };
    }
}