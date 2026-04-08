using System;
using System.Text.Json.Serialization;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Scheduling;

internal class ScheduleTaskDataItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("schedule")]
    public string Schedule { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("executionCount")]
    public long ExecutionCount { get; set; }

    [JsonPropertyName("lastExecution")]
    public long LastExecution { get; set; }

    [JsonPropertyName("lastExecutionResult")]
    public bool? LastExecutionResult { get; set; }

    [JsonPropertyName("nextExecution")]
    public long NextExecution { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; }

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("errorQueue")]
    public string ErrorQueue { get; set; }

    public static ScheduleTaskDataItem Create(ScheduledTask task)
    {
        return new ScheduleTaskDataItem
        {
            Name = task.Name,
            Category = task.Category,
            Schedule = task.Schedule,
            IsEnabled = task.IsEnabled,
            ExecutionCount = task.ExecutionCount,
            LastExecution = task.LastExecution?.ToUnixMilliseconds() ?? 0,
            LastExecutionResult = task.LastExecutionResult,
            NextExecution = task.NextExecution.ToUnixMilliseconds(),
            Type = task.Type.ToString(),
            Target = task.Target,
            Parameters = task.Parameters,
            RetryCount = task.RetryCount,
            ErrorQueue = task.ErrorQueue
        };
    }

    public ScheduledTask GetTask()
    {
        return new ScheduledTask
        {
            Name = Name,
            Category = Category,
            Schedule = Schedule,
            IsEnabled = IsEnabled,
            ExecutionCount = ExecutionCount,
            LastExecution = LastExecution > 0 ? LastExecution.ToUnixDate() : null,
            LastExecutionResult = LastExecutionResult,
            NextExecution = NextExecution > 0 ? NextExecution.ToUnixDate() : new DateTime(),
            Type = (ScheduleType)Enum.Parse(typeof(ScheduleType), Type),
            Target = Target,
            Parameters = Parameters,
            RetryCount = RetryCount,
            ErrorQueue = ErrorQueue
        };
    }
}