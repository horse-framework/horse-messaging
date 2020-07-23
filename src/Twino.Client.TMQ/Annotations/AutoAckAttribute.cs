using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used on Consumer Interfaces.
    /// Sends acknowledge for the message if it's required after consume operation completed successfuly
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoAckAttribute : Attribute
    {
    }
}