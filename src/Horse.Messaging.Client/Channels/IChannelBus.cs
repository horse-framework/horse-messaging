using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Messager implementation for Horse Channels
    /// </summary>
    public interface IChannelBus
    {

        Task<HorseResult> Create(string channel, Action<ChannelOptions> options = null);

        Task<HorseResult> Delete(string channel);
        
        
        Task<HorseResult> Publish<TModel>(TModel model);

        Task<HorseResult> Publish<TModel>(string channel, TModel model);

        Task<HorseResult> Publish(string channel, string message);
        
        Task<HorseResult> Publish(string channel, byte[] data);
    }
}