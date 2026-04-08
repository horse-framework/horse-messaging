namespace Horse.Messaging.Server.Scheduling;

/// <summary>
/// Type of the scheduled task.
/// </summary>
public enum ScheduleType : byte
{
    /// <summary>
    /// Executes an HTTP request.
    /// </summary>
    HttpRequest,

    /// <summary>
    /// Executes a plugin request.
    /// </summary>
    PluginExecute,

    /// <summary>
    /// Pushes a message to a queue.
    /// </summary>
    QueuePush,

    /// <summary>
    /// Publishes a message to a router.
    /// </summary>
    RouterPublish
}