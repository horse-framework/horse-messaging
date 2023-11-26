using Horse.Messaging.Protocol;

namespace Horse.Messaging.Data;

/// <summary>
/// Database file message object
/// </summary>
public class DataMessage
{
    /// <summary>
    /// Message data type
    /// </summary>
    public readonly DataType Type;

    /// <summary>
    /// Message id
    /// </summary>
    public readonly string Id;

    /// <summary>
    /// Horse Message itself
    /// </summary>
    public readonly HorseMessage Message;

    /// <summary>
    /// Creates new data message for database IO operations
    /// </summary>
    public DataMessage(DataType type, string id, HorseMessage message = null)
    {
        Type = type;
        Id = id;
        Message = message;
    }
}