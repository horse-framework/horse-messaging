using System;

namespace Horse.Messaging.Client.Annotations;

/// <summary>
/// Pushes exceptions to queues when thrown by consumer objects
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PushExceptionsAttribute : Attribute
{
    /// <summary>
    /// Exception type
    /// </summary>
    public Type ExceptionType { get; }
        
    /// <summary>
    /// Exception model type
    /// </summary>
    public Type ModelType { get; }
        
    /// <summary>
    /// Pushes all exceptions
    /// </summary>
    public PushExceptionsAttribute(Type modelType)
    {
        ModelType = modelType;
    }

    /// <summary>
    /// Pushes specified type of exceptions
    /// </summary>
    public PushExceptionsAttribute(Type modelType, Type exceptionType)
    {
        ModelType = modelType;
        ExceptionType = exceptionType;
    }
}