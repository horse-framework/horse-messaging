using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Scheduling;

/// <summary>
/// Manages scheduled tasks and their executions.
/// </summary>
public class ScheduleRider
{
    #region Definitions

    private ScheduledTask[] _tasks = [];

    /// <summary>
    /// Horse rider object for server functions.
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Creates a new schedule rider.
    /// </summary>
    public ScheduleRider(HorseRider rider)
    {
        Rider = rider;
    }

    #endregion

    #region Init - Run

    /// <summary>
    /// Initializes the schedule rider.
    /// Loads tasks and starts the runner loop.
    /// </summary>
    public void Initialize()
    {
        try
        {
            Load();
            _ = Run();
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleLoad, "ScheduleRider.Initialize", e);
        }
    }

    private async Task Run()
    {
        bool stopSubscription = false;
        CancellationTokenSource cts = new CancellationTokenSource();

        //wait for start
        while (Rider.Server == null || Rider.Server is { IsRunning: false })
        {
            if (Rider.Server != null && !stopSubscription)
            {
                stopSubscription = true;
                Rider.Server?.OnStopped += s => cts.Cancel();
            }

            await Task.Delay(100, cts.Token);
        }

        //run while running
        while (Rider.Server.IsRunning)
        {
            try
            {
                int waitDelay = 2000;
                DateTime now = DateTime.UtcNow;
                ScheduledTask[] tasks = _tasks;

                foreach (ScheduledTask task in tasks)
                {
                    if (!task.IsEnabled)
                        continue;

                    if (task.NextExecution <= now)
                    {
                        _ = RunTask(task);
                    }

                    TimeSpan leftDuration = task.NextExecution - now;
                    if (leftDuration.TotalMilliseconds > 0 && leftDuration.TotalMilliseconds < waitDelay)
                        waitDelay = Convert.ToInt32(leftDuration.TotalMilliseconds);
                }

                if (waitDelay < 100)
                    waitDelay = 100;

                await Task.Delay(waitDelay, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleSystemRun, "ScheduleRider.Run", e);
                await Task.Delay(1000, cts.Token);
            }
        }
    }

    private async Task RunTask(ScheduledTask task)
    {
        try
        {
            task.NextExecution = DateTime.MaxValue;
            int retryCount = task.RetryCount;
            do
            {
                try
                {
                    await task.Execute(Rider);
                    task.LastExecutionResult = true;
                    break;
                }
                catch
                {
                    retryCount--;
                    if (retryCount < 0)
                        throw;

                    await Task.Delay(Math.Max(250, (task.RetryCount - retryCount) * 250));
                }
            } while (true);
        }
        catch (Exception e)
        {
            task.LastExecutionResult = false;

            if (!string.IsNullOrEmpty(task.ErrorQueue))
            {
                try
                {
                    HorseQueue queue = Rider.Queue.Find(task.ErrorQueue);

                    if (queue == null)
                        queue = await Rider.Queue.Create(task.ErrorQueue);

                    if (queue != null)
                    {
                        var messageItem = ScheduleTaskDataItem.Create(task);
                        HorseMessage message = new HorseMessage(MessageType.QueueMessage, task.ErrorQueue);
                        message.SetStringContent(JsonSerializer.Serialize(messageItem));
                        message.SetStringAdditionalContent(JsonSerializer.Serialize(new
                        {
                            message = e.Message,
                            stackTrace = e.StackTrace
                        }));

                        await queue.Push(message);
                    }
                }
                catch
                {
                    // Ignore error queue push errors to not break the flow
                }
            }

            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ScheduleTaskRun, "ScheduleRider.RunTask", e);
        }

        task.ExecutionCount++;
        task.LastExecution = DateTime.UtcNow;
        task.NextExecution = CalculateNextExecutionTime(task);
    }

    private DateTime CalculateNextExecutionTime(ScheduledTask task)
    {
        try
        {
            CronExpression expression = CronExpression.Parse(task.Schedule, CronFormat.Standard);
            DateTime? next = expression.GetNextOccurrence(task.LastExecution ?? DateTime.UtcNow);
            if (next.HasValue)
                return next.Value;

            task.IsEnabled = false;
            return DateTime.UtcNow;
        }
        catch (Exception e)
        {
            task.IsEnabled = false;
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ScheduleTimeCalculation, "ScheduleRider.CalculateNextExecutionTime", e);
            return DateTime.UtcNow;
        }
    }

    #endregion

    #region Load - Save

    private void Load()
    {
        string filename = Path.Combine(Rider.Options.DataPath, "tasks.json");

        if (!File.Exists(filename))
        {
            _tasks = [];
            return;
        }

        string json = File.ReadAllText(filename);
        ScheduleTaskDataItem[] items = JsonSerializer.Deserialize<ScheduleTaskDataItem[]>(json);
        _tasks = items.Select(x => x.GetTask()).ToArray();
        foreach (ScheduledTask scheduledTask in _tasks)
            scheduledTask.NextExecution = CalculateNextExecutionTime(scheduledTask);
    }

    private void Save()
    {
        try
        {
            ScheduledTask[] tasks = _tasks;

            if (tasks == null)
                return;

            var items = tasks
                .Where(x => x != null)
                .Select(ScheduleTaskDataItem.Create)
                .ToList();

            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string filename = Path.Combine(Rider.Options.DataPath, "tasks.json");
            File.WriteAllText(filename, json);
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleSave, "ScheduleRider.Save", e);
        }
    }

    #endregion

    #region CRUD

    /// <summary>
    /// Returns all scheduled tasks.
    /// </summary>
    public List<ScheduledTask> GetTasks()
    {
        ScheduledTask[] array = _tasks;
        return array == null ? [] : array.ToList();
    }

    /// <summary>
    /// Adds a new scheduled task.
    /// Returns null if a task with the same name already exists.
    /// </summary>
    public ScheduledTask AddTask(string name, string category, string schedule, ScheduleType type,
        string target, string parameters, string errorQueue = null,
        int retryCount = 0)
    {
        ScheduledTask previous = _tasks.FirstOrDefault(x => x.Name == name);
        if (previous != null)
            return null;

        ScheduledTask task = new ScheduledTask
        {
            Name = name,
            Category = category,
            Schedule = schedule,
            Type = type,
            Target = target,
            Parameters = parameters,
            RetryCount = retryCount,
            ErrorQueue = errorQueue,
            IsEnabled = true
        };

        lock (_tasks)
        {
            previous = _tasks.FirstOrDefault(x => x.Name == name);
            if (previous != null)
                return null;

            var list = _tasks.ToList();
            list.Add(task);
            _tasks = list.ToArray();
        }

        task.NextExecution = CalculateNextExecutionTime(task);
        Save();

        return task;
    }

    /// <summary>
    /// Removes a scheduled task by name.
    /// </summary>
    public void RemoveTask(string name)
    {
        ScheduledTask task = _tasks.FirstOrDefault(x => x.Name == name);
        if (task == null)
            return;

        lock (_tasks)
        {
            var list = _tasks.ToList();
            list.Remove(task);
            _tasks = list.ToArray();
        }

        Save();
    }

    /// <summary>
    /// Updates an existing scheduled task.
    /// Returns false if the task is not found.
    /// </summary>
    public bool UpdateTask(string name, string category, string schedule, ScheduleType type,
        string target, string parameters, string errorQueue = null,
        int retryCount = 0)
    {
        ScheduledTask task = _tasks.FirstOrDefault(x => x.Name == name);
        if (task == null)
            return false;

        task.Name = name;
        task.Category = category;
        task.Schedule = schedule;
        task.Type = type;
        task.Target = target;
        task.Parameters = parameters;
        task.RetryCount = retryCount;
        task.ErrorQueue = errorQueue;
        task.NextExecution = CalculateNextExecutionTime(task);

        Save();
        return true;
    }

    /// <summary>
    /// Sets the enabled status of a scheduled task.
    /// Returns false if the task is not found.
    /// </summary>
    public bool SetStatus(string name, bool isEnabled)
    {
        ScheduledTask task = _tasks.FirstOrDefault(x => x.Name == name);
        if (task == null)
            return false;

        task.IsEnabled = isEnabled;
        task.NextExecution = isEnabled ? CalculateNextExecutionTime(task) : DateTime.MaxValue;

        Save();
        return true;
    }

    /// <summary>
    /// Clears all scheduled tasks.
    /// </summary>
    public void ClearTasks()
    {
        _tasks = [];
        Save();
    }

    #endregion
}