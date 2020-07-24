namespace Twino.Client.TMQ
{
    /// <summary>
    /// Queue Consumer implementation.
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
#pragma warning disable 618
    public interface IQueueConsumer<in TModel> : ITwinoConsumer<TModel>
#pragma warning restore 618
    {
    }
}