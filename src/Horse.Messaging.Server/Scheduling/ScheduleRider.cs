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
using Horse.Server;

namespace Horse.Messaging.Server.Scheduling;

/// <summary>
/// Manages scheduled tasks and their executions.
/// </summary>
public class ScheduleRider
{
    #region Definitions

    private const string TasksFilename = "tasks.json";

    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    /// <summary>
    /// Task list. It's replaced as a whole on changes, readers take it without a lock.
    /// </summary>
    private volatile ScheduledTask[] _tasks = [];

    /// <summary>
    /// Guards changes of the task list and the task definitions.
    /// The list can't be the lock object: it is replaced inside the lock, so callers coming later locked another object.
    /// </summary>
    private readonly object _tasksLock = new();

    /// <summary>
    /// Serializes writes of the tasks file. Concurrent writes could interleave and corrupt the file.
    /// </summary>
    private readonly object _saveLock = new();

    /// <summary>
    /// Guards attaching to the server and starting or stopping the runner
    /// </summary>
    private readonly object _lifecycleLock = new();

    private HorseServer _attachedServer;
    private CancellationTokenSource _runnerCts;

    /// <summary>
    /// 1 when a task execution changed the task state after the last save
    /// </summary>
    private int _dirty;

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
    /// Loads tasks and runs them while the server is running.
    /// </summary>
    public void Initialize()
    {
        try
        {
            Load();
        }
        catch (Exception e)
        {
            //Load handles a damaged file itself. The runner starts in any case, it used to never start after a load error.
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleLoad, "ScheduleRider.Initialize", e);
        }

        AttachServer();
    }

    /// <summary>
    /// Follows the start and stop of the rider's server.
    /// Tasks run while the server is running, a stopped server can be started again.
    /// Called again when a pre-built rider receives its server.
    /// </summary>
    internal void AttachServer()
    {
        lock (_lifecycleLock)
        {
            HorseServer server = Rider.Server;
            if (!ReferenceEquals(_attachedServer, server))
            {
                if (_attachedServer != null)
                {
                    _attachedServer.OnStarted -= OnServerStarted;
                    _attachedServer.OnStopped -= OnServerStopped;
                    StopRunner();
                }

                _attachedServer = server;

                if (server != null)
                {
                    server.OnStarted += OnServerStarted;
                    server.OnStopped += OnServerStopped;
                }
            }

            if (server is { IsRunning: true })
                StartRunner();
        }
    }

    private void OnServerStarted(HorseServer server)
    {
        lock (_lifecycleLock)
        {
            if (ReferenceEquals(server, _attachedServer))
                StartRunner();
        }
    }

    private void OnServerStopped(HorseServer server)
    {
        lock (_lifecycleLock)
        {
            if (!ReferenceEquals(server, _attachedServer))
                return;

            StopRunner();
        }

        //the runner saves when it ends too, the state is kept here before the host process may exit
        SaveIfDirty();
    }

    /// <summary>
    /// Starts the runner if it's not running. Called in the lifecycle lock.
    /// </summary>
    private void StartRunner()
    {
        if (_runnerCts != null)
            return;

        CancellationTokenSource cts = new CancellationTokenSource();
        _runnerCts = cts;
        _ = Run(cts);
    }

    /// <summary>
    /// Stops the runner. Running HTTP requests and retry delays are cancelled. Called in the lifecycle lock.
    /// </summary>
    private void StopRunner()
    {
        CancellationTokenSource cts = _runnerCts;
        if (cts == null)
            return;

        _runnerCts = null;
        cts.Cancel();
    }

    private async Task Run(CancellationTokenSource cts)
    {
        CancellationToken cancellationToken = cts.Token;
        DateTime lastSave = DateTime.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int waitDelay = 2000;

                try
                {
                    DateTime now = DateTime.UtcNow;

                    foreach (ScheduledTask task in _tasks)
                    {
                        if (!task.IsEnabled)
                            continue;

                        //a task that is still running (long execution, retries, or re-enabled meanwhile) is not started again
                        if (task.NextExecution <= now && task.TryBeginRun())
                            _ = RunTask(task, cancellationToken);

                        TimeSpan leftDuration = task.NextExecution - now;
                        if (leftDuration.TotalMilliseconds > 0 && leftDuration.TotalMilliseconds < waitDelay)
                            waitDelay = Convert.ToInt32(leftDuration.TotalMilliseconds);
                    }
                }
                catch (Exception e)
                {
                    Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleSystemRun, "ScheduleRider.Run", e);
                    waitDelay = 1000;
                }

                //only executions change the state here, definition changes are saved by the CRUD methods
                if (DateTime.UtcNow - lastSave > TimeSpan.FromSeconds(60))
                {
                    SaveIfDirty();
                    lastSave = DateTime.UtcNow;
                }

                await Task.Delay(Math.Clamp(waitDelay, 100, 2000), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
            SaveIfDirty();
        }
    }

    private async Task RunTask(ScheduledTask task, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteTask(task, cancellationToken);
        }
        finally
        {
            task.NextExecution = CalculateNextExecutionTime(task);
            task.EndRun();
            Volatile.Write(ref _dirty, 1);
        }
    }

    private async Task ExecuteTask(ScheduledTask task, CancellationToken cancellationToken)
    {
        try
        {
            task.NextExecution = DateTime.MaxValue;
            int retryCount = task.RetryCount;
            do
            {
                try
                {
                    await task.Execute(Rider, cancellationToken);
                    task.LastExecutionResult = true;
                    task.ExecutionCount++;
                    break;
                }
                catch when (!cancellationToken.IsCancellationRequested)
                {
                    retryCount--;
                    if (retryCount < 0)
                        throw;

                    await Task.Delay(Math.Max(250, (task.RetryCount - retryCount) * 250), cancellationToken);
                }
            } while (true);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            //the server is stopped during the execution, it's not a failure of the task
            return;
        }
        catch (Exception e)
        {
            task.LastExecutionResult = false;
            task.SkipCount++;

            if (!string.IsNullOrEmpty(task.SkipQueue))
            {
                try
                {
                    HorseQueue queue = Rider.Queue.Find(task.SkipQueue);

                    if (queue == null)
                        queue = await Rider.Queue.Create(task.SkipQueue);

                    if (queue != null)
                    {
                        var messageItem = ScheduleTaskDataItem.Create(task);
                        HorseMessage message = new HorseMessage(MessageType.QueueMessage, task.SkipQueue);
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

        task.LastExecution = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the next execution time of the schedule.
    /// Returns null if the schedule has no next execution, throws if the schedule is not valid.
    /// </summary>
    private static DateTime? GetNextExecution(string schedule, DateTime? lastExecution)
    {
        if (schedule.Length >= 3 && schedule.StartsWith("s/"))
        {
            int everySecondsValue = Convert.ToInt32(schedule[2..].Split(' ')[0].Trim());
            return DateTime.UtcNow.AddSeconds(everySecondsValue);
        }

        CronExpression expression = CronExpression.Parse(schedule, CronFormat.Standard);
        return expression.GetNextOccurrence(lastExecution ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Returns true if the schedule can be parsed and has a next execution
    /// </summary>
    private static bool IsValidSchedule(string schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule))
            return false;

        try
        {
            return GetNextExecution(schedule, null).HasValue;
        }
        catch
        {
            return false;
        }
    }

    private DateTime CalculateNextExecutionTime(ScheduledTask task)
    {
        try
        {
            DateTime? next = GetNextExecution(task.Schedule, task.LastExecution);
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

    private string GetFilename() => Path.Combine(Rider.Options.DataPath, TasksFilename);

    /// <summary>
    /// Loads the tasks one by one. A task that can't be read is skipped and reported,
    /// a damaged file is copied aside before the next save rewrites it without the skipped tasks.
    /// </summary>
    private void Load()
    {
        string filename = GetFilename();

        if (!File.Exists(filename))
        {
            lock (_tasksLock)
                _tasks = [];

            return;
        }

        List<ScheduledTask> tasks = new List<ScheduledTask>();
        bool damaged = false;

        try
        {
            //read as text, a hand edited file may start with a byte order mark
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filename));

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException($"Root of {filename} is not an array");

            int index = 0;
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                try
                {
                    ScheduleTaskDataItem item = element.Deserialize<ScheduleTaskDataItem>();
                    if (item == null)
                        throw new JsonException("Task item is null");

                    tasks.Add(item.GetTask());
                }
                catch (Exception e)
                {
                    damaged = true;

                    string name = element.ValueKind == JsonValueKind.Object &&
                                  element.TryGetProperty("name", out JsonElement nameElement) &&
                                  nameElement.ValueKind == JsonValueKind.String
                        ? nameElement.GetString()
                        : null;

                    Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ScheduleLoad,
                        $"Scheduled task #{index} ({name ?? "no name"}) in {filename} can't be loaded and is skipped", e);
                }

                index++;
            }
        }
        catch (Exception e)
        {
            damaged = true;
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleLoad, $"Scheduled tasks file {filename} can't be read", e);
        }

        if (damaged)
            BackupDamagedFile(filename);

        foreach (ScheduledTask task in tasks)
            task.NextExecution = CalculateNextExecutionTime(task);

        lock (_tasksLock)
            _tasks = tasks.ToArray();
    }

    private void BackupDamagedFile(string filename)
    {
        try
        {
            string backup = $"{filename}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.corrupt";
            File.Copy(filename, backup, false);

            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ScheduleLoad,
                $"Damaged scheduled tasks file is copied to {backup}. The tasks that can't be loaded are removed from {filename} on the next save.", null);
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleLoad, $"Damaged scheduled tasks file {filename} can't be copied aside", e);
        }
    }

    private void SaveIfDirty()
    {
        if (Volatile.Read(ref _dirty) == 1)
            Save();
    }

    /// <summary>
    /// Writes all tasks to the tasks file.
    /// Saves are serialized and the file is replaced by a fully written temporary file,
    /// a crash or a concurrent save can't leave a partial file.
    /// If the save fails, the runner tries again later.
    /// </summary>
    private void Save()
    {
        lock (_saveLock)
        {
            //cleared before the tasks are read: an execution finishing during the save marks it again
            Volatile.Write(ref _dirty, 0);

            try
            {
                //read in the lock, a save that waited for another one writes the latest state
                ScheduledTask[] tasks = _tasks;

                List<ScheduleTaskDataItem> items = new List<ScheduleTaskDataItem>(tasks.Length);
                foreach (ScheduledTask task in tasks)
                    if (task != null)
                        items.Add(ScheduleTaskDataItem.Create(task));

                byte[] json = JsonSerializer.SerializeToUtf8Bytes(items, SaveOptions);

                string filename = GetFilename();
                string tempFilename = filename + ".tmp";

                using (FileStream stream = new FileStream(tempFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(json);
                    stream.Flush(true);
                }

                File.Move(tempFilename, filename, true);
            }
            catch (Exception e)
            {
                Volatile.Write(ref _dirty, 1);
                Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.ScheduleSave, "ScheduleRider.Save", e);
            }
        }
    }

    #endregion

    #region CRUD

    /// <summary>
    /// Returns all scheduled tasks.
    /// </summary>
    public List<ScheduledTask> GetTasks()
    {
        return _tasks.ToList();
    }

    /// <summary>
    /// Adds a new scheduled task.
    /// Returns null if a task with the same name already exists or the schedule is not valid
    /// (it can't be parsed or it has no next execution).
    /// </summary>
    public ScheduledTask AddTask(string name, string category, string schedule, ScheduleType type,
        string target, string parameters, string errorQueue = null,
        int retryCount = 0)
    {
        //an invalid schedule used to be added and saved as a disabled task while the caller got it as added
        if (!IsValidSchedule(schedule))
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
            SkipQueue = errorQueue,
            IsEnabled = true
        };

        //calculated before the task is visible to the runner, it used to see the default (past) time and run the task at once
        task.NextExecution = CalculateNextExecutionTime(task);

        lock (_tasksLock)
        {
            ScheduledTask[] tasks = _tasks;
            if (tasks.Any(x => x.Name == name))
                return null;

            _tasks = [..tasks, task];
        }

        Save();
        return task;
    }

    /// <summary>
    /// Removes a scheduled task by name.
    /// </summary>
    public void RemoveTask(string name)
    {
        lock (_tasksLock)
        {
            ScheduledTask[] tasks = _tasks;
            int index = Array.FindIndex(tasks, x => x.Name == name);
            if (index < 0)
                return;

            ScheduledTask[] list = new ScheduledTask[tasks.Length - 1];
            Array.Copy(tasks, 0, list, 0, index);
            Array.Copy(tasks, index + 1, list, index, tasks.Length - index - 1);
            _tasks = list;
        }

        Save();
    }

    /// <summary>
    /// Updates an existing scheduled task.
    /// Returns false if the task is not found or the schedule is not valid
    /// (it can't be parsed or it has no next execution).
    /// The enabled status is not changed, use SetStatus for it.
    /// </summary>
    public bool UpdateTask(string name, string category, string schedule, ScheduleType type,
        string target, string parameters, string errorQueue = null,
        int retryCount = 0)
    {
        if (!IsValidSchedule(schedule))
            return false;

        lock (_tasksLock)
        {
            ScheduledTask task = _tasks.FirstOrDefault(x => x.Name == name);
            if (task == null)
                return false;

            task.Category = category;
            task.Schedule = schedule;
            task.Type = type;
            task.Target = target;
            task.Parameters = parameters;
            task.RetryCount = retryCount;
            task.SkipQueue = errorQueue;
            task.NextExecution = CalculateNextExecutionTime(task);
        }

        Save();
        return true;
    }

    /// <summary>
    /// Sets the enabled status of a scheduled task.
    /// Returns false if the task is not found.
    /// </summary>
    public bool SetStatus(string name, bool isEnabled)
    {
        lock (_tasksLock)
        {
            ScheduledTask task = _tasks.FirstOrDefault(x => x.Name == name);
            if (task == null)
                return false;

            task.IsEnabled = isEnabled;
            task.NextExecution = isEnabled ? CalculateNextExecutionTime(task) : DateTime.MaxValue;
        }

        Save();
        return true;
    }

    /// <summary>
    /// Clears all scheduled tasks.
    /// </summary>
    public void ClearTasks()
    {
        lock (_tasksLock)
            _tasks = [];

        Save();
    }

    #endregion
}
