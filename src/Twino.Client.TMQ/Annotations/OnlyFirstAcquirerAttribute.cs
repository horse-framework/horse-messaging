using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// The types have that attribute are consumed by only one (first) consumer
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OnlyFirstAcquirerAttribute : Attribute
    {
    }
}