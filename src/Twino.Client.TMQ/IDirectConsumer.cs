using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Directmessage Consumer implementation.
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
#pragma warning disable 618
    public interface IDirectConsumer<in TModel> : ITwinoConsumer<TModel>
#pragma warning restore 618
    {
    }
}