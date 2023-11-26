using System;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Default Unique Id generator
/// </summary>
public class DefaultUniqueIdGenerator : IUniqueIdGenerator
{
    /// <summary>
    /// Generates unique id. Uses Guid.
    /// </summary>
    public string Create()
    {
        return Guid.NewGuid().ToString("N");
    }
}