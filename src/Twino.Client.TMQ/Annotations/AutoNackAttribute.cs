using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used on Consumer Interfaces.
    /// Sends negative acknowledge for the message if it's required after consume operation throws an exception
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoNackAttribute : Attribute
    {
    }
}