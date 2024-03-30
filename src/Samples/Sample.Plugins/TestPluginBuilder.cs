using Horse.Messaging.Plugins;

namespace Sample.Plugins;

public class TestPluginBuilder : IHorsePluginBuilder
{
    public string GetName() => "Test";

    public HorsePlugin Build()
    {
        return new TestPlugin();
    }
}