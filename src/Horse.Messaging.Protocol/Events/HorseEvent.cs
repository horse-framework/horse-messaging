using System;
using System.Collections.Generic;

namespace Horse.Messaging.Protocol.Events;

/// <summary>
/// Horse event model
/// </summary>
public class HorseEvent
{
    /// <summary>
    /// Event type
    /// </summary>
    public HorseEventType Type { get; set; }

    /// <summary>
    /// Event subject client
    /// </summary>
    public EventSubject Subject { get; set; }

    /// <summary>
    /// Event target
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Event parameters
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> Parameters { get; set; }

    /// <summary>
    /// Finds parameter value by key (case insensitive).
    /// Returns null if the parameter is not found.
    /// </summary>
    public string FindParameter(string key)
    {
        if (Parameters == null)
            return null;

        foreach (KeyValuePair<string, string> parameter in Parameters)
        {
            if (string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))
                return parameter.Value;
        }

        return null;
    }
}