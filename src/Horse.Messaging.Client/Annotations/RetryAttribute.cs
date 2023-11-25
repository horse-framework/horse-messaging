using System;

namespace Horse.Messaging.Client.Annotations;

/// <summary>
/// Retry attribute for consumers.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RetryAttribute : Attribute
{
    /// <summary>
    /// Retry count.
    /// Zero is max.
    /// To prevent infinitive loops max retry count is 100.
    /// </summary>
    public int Count { get; set; } = 5;

    /// <summary>
    /// Delay between retries in milliseconds.
    /// </summary>
    public int DelayBetweenRetries { get; set; } = 50;

    /// <summary>
    /// Ignored exceptions
    /// </summary>
    public Type[] IgnoreExceptions { get; set; }

    /// <summary>
    /// Creates new retry attribute
    /// </summary>
    public RetryAttribute()
    {
    }

    /// <summary>
    /// Creates new retry attribute
    /// </summary>
    public RetryAttribute(int count, int delayBetweenRetries = 50, params Type[] ignoreExceptions)
    {
        Count = count;
        DelayBetweenRetries = delayBetweenRetries;
        IgnoreExceptions = ignoreExceptions;
    }
}