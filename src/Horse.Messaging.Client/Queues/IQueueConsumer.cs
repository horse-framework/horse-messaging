using System.Threading.Tasks;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Queue Consumer implementation.
/// </summary>
/// <typeparam name="TModel">Model type</typeparam>
public interface IQueueConsumer<TModel>
{
    /// <summary>
    /// Consumes a message
    /// </summary>
    /// <param name="context">Consume context</param>
    Task Consume(ConsumeContext<TModel> context);
}