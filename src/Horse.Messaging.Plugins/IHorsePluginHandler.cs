using System.Threading.Tasks;

namespace Horse.Messaging.Plugins;

public interface IHorsePluginHandler
{
    public Task Execute(HorsePluginContext context);
}