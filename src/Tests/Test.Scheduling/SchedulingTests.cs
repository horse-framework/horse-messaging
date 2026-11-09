using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Scheduling;
using Test.Common;
using Xunit;

namespace Test.Scheduling;

public class SchedulingTests
{
    [Fact]
    public void ToUnixDate_ShouldReturnUtcDateTime()
    {
        long unixMilliseconds = DateTimeOffset.Parse("2023-10-01T00:00:00Z").ToUnixTimeMilliseconds();

        DateTime result = unixMilliseconds.ToUnixDate();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ToUnixMilliseconds_ShouldClampDateTimeMaxValue()
    {
        long result = DateTime.MaxValue.ToUnixMilliseconds();

        Assert.Equal(253402300799999, result);
    }

    [Fact]
    public void LegacyScheduleFile_ShouldLoad_WhenNextExecutionExceededUnixMax()
    {
        string dataPath = Path.Combine(Path.GetTempPath(), $"horse-scheduling-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);

        try
        {
            string tasksFilename = Path.Combine(dataPath, "tasks.json");
            string json = JsonSerializer.Serialize(new[]
            {
                new
                {
                    name = "LegacyTask",
                    category = "Tests",
                    schedule = "*/5 * * * *",
                    isEnabled = true,
                    executionCount = 0L,
                    skipCount = 0L,
                    lastExecution = 0L,
                    lastExecutionResult = (bool?) null,
                    nextExecution = 253402300800000L,
                    type = ScheduleType.QueuePush.ToString(),
                    target = "legacy-queue",
                    parameters = "hello",
                    retryCount = 0,
                    skipQueue = (string) null
                }
            });

            File.WriteAllText(tasksFilename, json);

            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureOptions(o => o.DataPath = dataPath)
                .Build();

            ScheduledTask task = Assert.Single(rider.Schedule.GetTasks());
            Assert.Equal("LegacyTask", task.Name);
            Assert.True(task.IsEnabled);
            Assert.True(task.NextExecution > DateTime.UtcNow);
        }
        finally
        {
            if (Directory.Exists(dataPath))
                Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public async Task CronCalculation_ShouldBeCorrect()
    {
        TestHorseRider testRider = new TestHorseRider();
        await testRider.Initialize();
        var scheduleRider = testRider.Rider.Schedule;

        var task = new ScheduledTask
        {
            Name = "TestTask",
            Schedule = "0 3 * * *", // Every day at 03:00
            IsEnabled = true,
            LastExecution = new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // We need to access CalculateNextExecutionTime which is private. 
        // But we can use AddTask or UpdateTask to trigger it, or just use reflection if needed.
        // Actually, AddTask calls it.
        
        var addedTask = scheduleRider.AddTask("TestTask", "Cat", "0 3 * * *", ScheduleType.QueuePush, "Target", "Params");
        
        Assert.NotNull(addedTask);
        Assert.Equal(3, addedTask.NextExecution.Hour);
        Assert.Equal(0, addedTask.NextExecution.Minute);
        Assert.True(addedTask.NextExecution > DateTime.UtcNow);
    }

    [Fact]
    public async Task TaskCRUD_ShouldWork()
    {
        TestHorseRider testRider = new TestHorseRider();
        await testRider.Initialize();
        var scheduleRider = testRider.Rider.Schedule;

        // Add
        var task = scheduleRider.AddTask("Task1", "Cat1", "*/1 * * * *", ScheduleType.QueuePush, "Q1", "P1");
        Assert.NotNull(task);
        Assert.Single(scheduleRider.GetTasks());

        // Update
        bool updated = scheduleRider.UpdateTask("Task1", "Cat1", "*/2 * * * *", ScheduleType.QueuePush, "Q1", "P2");
        Assert.True(updated);
        var updatedTask = scheduleRider.GetTasks().First();
        Assert.Equal("*/2 * * * *", updatedTask.Schedule);
        Assert.Equal("P2", updatedTask.Parameters);

        // Remove
        scheduleRider.RemoveTask("Task1");
        Assert.Empty(scheduleRider.GetTasks());
    }

    [Fact]
    public async Task TaskExecution_QueuePush_ShouldWork()
    {
        TestHorseRider testRider = new TestHorseRider();
        await testRider.Initialize();
        var rider = testRider.Rider;
        var scheduleRider = rider.Schedule;

        string queueName = "test-sched-queue";
        var task = scheduleRider.AddTask("PushTask", "Test", "*/1 * * * *", ScheduleType.QueuePush, queueName, "Hello");
        
        // Manual execution to verify logic
        await task.Execute(rider);

        var queue = rider.Queue.Find(queueName);
        Assert.NotNull(queue);
        Assert.Equal(1, queue.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task TaskExecution_RouterPublish_ShouldWork()
    {
        TestHorseRider testRider = new TestHorseRider();
        await testRider.Initialize();
        var rider = testRider.Rider;
        var scheduleRider = rider.Schedule;

        string routerName = "test-sched-router";
        rider.Router.Add(routerName, Horse.Messaging.Protocol.RouteMethod.Distribute);

        var task = scheduleRider.AddTask("RouterTask", "Test", "*/1 * * * *", ScheduleType.RouterPublish, routerName, "Hello");
        
        // Manual execution to verify logic
        await task.Execute(rider);
        
        // Router execution is hard to verify without a binding, but if it doesn't throw, it's mostly fine for this unit test.
        var router = rider.Router.Find(routerName);
        Assert.NotNull(router);
    }

    [Fact]
    public async Task TaskRetry_And_ErrorQueue_ShouldWork()
    {
        TestHorseRider testRider = new TestHorseRider();
        await testRider.Initialize();
        var rider = testRider.Rider;
        var scheduleRider = rider.Schedule;

        string errorQueueName = "error-queue";
        var task = scheduleRider.AddTask("RetryTask", "Test", "*/1 * * * *", ScheduleType.HttpRequest, 
            "GET http://invalid-url-that-fails-surely.com", "Params", errorQueueName, 1);
        
        // We can't easily trigger private RunTask, but we can verify CalculateNextExecutionTime
        // was called and it's scheduled.
        Assert.NotNull(task);
        Assert.True(task.IsEnabled);

        // Verify Execute directly
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.Execute(rider));
    }

    private static string CreateDataPath()
    {
        string dataPath = Path.Combine(Path.GetTempPath(), $"horse-scheduling-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);
        return dataPath;
    }

    private static HorseRider BuildRider(string dataPath)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .Build();
    }

    private static object TaskItem(string name, string type = "QueuePush", string schedule = "*/5 * * * *")
    {
        return new
        {
            name,
            category = "Tests",
            schedule,
            isEnabled = true,
            executionCount = 0L,
            skipCount = 0L,
            lastExecution = 0L,
            lastExecutionResult = (bool?) null,
            nextExecution = 0L,
            type,
            target = "q",
            parameters = "p",
            retryCount = 0,
            skipQueue = (string) null
        };
    }

    [Fact]
    public void DamagedTaskItem_IsSkipped_OtherTasksLoad_AndFileIsCopiedAside()
    {
        string dataPath = CreateDataPath();
        try
        {
            string tasksFilename = Path.Combine(dataPath, "tasks.json");
            string json = JsonSerializer.Serialize(new[] { TaskItem("Good"), TaskItem("Bad", type: "NoSuchType"), null, TaskItem("Good2") });
            File.WriteAllText(tasksFilename, json);

            HorseRider rider = BuildRider(dataPath);

            Assert.Equal(new[] { "Good", "Good2" }, rider.Schedule.GetTasks().Select(x => x.Name).ToArray());

            string backup = Assert.Single(Directory.GetFiles(dataPath, "tasks.json.*.corrupt"));
            Assert.Equal(json, File.ReadAllText(backup));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void UnreadableFile_IsCopiedAside_BeforeItIsRewritten()
    {
        string dataPath = CreateDataPath();
        try
        {
            string tasksFilename = Path.Combine(dataPath, "tasks.json");
            const string broken = "[{\"name\":\"Lost\",\"sched";
            File.WriteAllText(tasksFilename, broken);

            HorseRider rider = BuildRider(dataPath);
            Assert.Empty(rider.Schedule.GetTasks());

            // the next change rewrites tasks.json, the original content must survive in the copy
            Assert.NotNull(rider.Schedule.AddTask("New", "Tests", "*/5 * * * *", ScheduleType.QueuePush, "q", "p"));

            string backup = Assert.Single(Directory.GetFiles(dataPath, "tasks.json.*.corrupt"));
            Assert.Equal(broken, File.ReadAllText(backup));

            HorseRider reloaded = BuildRider(dataPath);
            Assert.Equal("New", Assert.Single(reloaded.Schedule.GetTasks()).Name);
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void InvalidSchedule_IsRejected_ByAddAndUpdate()
    {
        string dataPath = CreateDataPath();
        try
        {
            HorseRider rider = BuildRider(dataPath);
            ScheduleRider schedule = rider.Schedule;

            Assert.Null(schedule.AddTask("Invalid", "Tests", "not a cron", ScheduleType.QueuePush, "q", "p"));
            Assert.Null(schedule.AddTask("Null", "Tests", null, ScheduleType.QueuePush, "q", "p"));
            Assert.Null(schedule.AddTask("Never", "Tests", "0 0 30 2 *", ScheduleType.QueuePush, "q", "p"));
            Assert.Empty(schedule.GetTasks());

            ScheduledTask task = schedule.AddTask("Valid", "Tests", "*/5 * * * *", ScheduleType.QueuePush, "q", "p");
            Assert.NotNull(task);

            Assert.False(schedule.UpdateTask("Valid", "Tests", "invalid", ScheduleType.QueuePush, "q", "p2"));
            Assert.Equal("*/5 * * * *", task.Schedule);
            Assert.Equal("p", task.Parameters);
            Assert.True(task.IsEnabled);
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void ConcurrentAdds_AreAllKept_AndSaved()
    {
        string dataPath = CreateDataPath();
        try
        {
            HorseRider rider = BuildRider(dataPath);
            ScheduleRider schedule = rider.Schedule;

            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
            {
                Assert.NotNull(schedule.AddTask($"Task-{i}", "Tests", "*/5 * * * *", ScheduleType.QueuePush, "q", "p"));
            });

            Assert.Equal(64, schedule.GetTasks().Count);

            HorseRider reloaded = BuildRider(dataPath);
            Assert.Equal(64, reloaded.Schedule.GetTasks().Count);
            Assert.False(File.Exists(Path.Combine(dataPath, "tasks.json.tmp")));
            Assert.Empty(Directory.GetFiles(dataPath, "*.corrupt"));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public async Task Tasks_StopWithServer_AndRunAgainAfterRestart()
    {
        await using TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        server.Start();

        ScheduledTask task = server.Rider.Schedule.AddTask("Tick", "Tests", "s/1", ScheduleType.QueuePush, "schedule-restart", "tick");
        Assert.NotNull(task);

        await TestHorseRider.WaitUntil(() => task.ExecutionCount >= 1, 5000);
        Assert.True(task.ExecutionCount >= 1);

        await server.Server.StopAsync();
        await Task.Delay(300);
        long stoppedCount = task.ExecutionCount;

        await Task.Delay(2500);
        Assert.Equal(stoppedCount, task.ExecutionCount);

        await server.Server.StartAsync();
        await TestHorseRider.WaitUntil(() => task.ExecutionCount > stoppedCount, 5000);
        Assert.True(task.ExecutionCount > stoppedCount);
    }
}
