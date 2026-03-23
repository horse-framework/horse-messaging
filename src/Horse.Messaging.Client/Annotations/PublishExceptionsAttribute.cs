using System;
using Horse.Messaging.Client.Queues;

namespace Horse.Messaging.Client.Annotations;

/// <summary>
/// Publishes exceptions to routers when thrown by consumer objects
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PublishExceptionsAttribute<T> : PublishExceptionsAttribute
    where T : ITransportableException, new()
{
    /// <summary>
    /// Publishes all exceptions
    /// </summary>
    public PublishExceptionsAttribute() : base(typeof(T))
    {
    }

    /// <summary>
    /// Publishes specified type of exceptions
    /// </summary>
    public PublishExceptionsAttribute(Type exceptionType) : base(typeof(T), exceptionType)
    {
    }
}

/// <summary>
/// Publishes exceptions to routers when thrown by consumer objects
/// </summary>
[Obsolete("Use PublishExceptionsAttribute<T> instead")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PublishExceptionsAttribute : Attribute
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
    /// Publishes all exceptions
    /// </summary>
    public PublishExceptionsAttribute(Type modelType)
    {
        ModelType = modelType;
    }

    /// <summary>
    /// Publishes specified type of exceptions
    /// </summary>
    public PublishExceptionsAttribute(Type modelType, Type exceptionType)
    {
        ModelType = modelType;
        ExceptionType = exceptionType;
    }
}