using System;
using System.Threading.Tasks;
using AdvancedSample.Service.Handlers;
using AdvancedSample.ServiceModels;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace TestService;

public class TestCommandHandler : CommandHandler<SampleTestCommand>
{
    protected override async Task Execute(SampleTestCommand command, HorseClient client)
    {
        using HorseTransaction transaction = new(client, "test");
        await transaction.Begin(new TestTransactionModel { Foo = transaction.Id });
        bool commited = await transaction.Commit();
        Console.WriteLine(commited);
    }
}

public class TestTransactionModel
{
    public string Foo { get; set; }
}

[QueueName("CommitQueue")]
public class CommitHandler : IQueueConsumer<TestTransactionModel>
{
    public Task Consume(HorseMessage message, TestTransactionModel model, HorseClient client)
    {
        return Task.CompletedTask;
    }
}