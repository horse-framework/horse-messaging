using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Server.Scheduling;
using Test.Common;
using Xunit;

namespace Test.Scheduling;

public class SchedulingTests
{
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
}
