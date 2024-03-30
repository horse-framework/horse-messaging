using Horse.Messaging.Plugins;

namespace Sample.Plugins;

public class TestPlugin : HorsePlugin
{
    public TestPlugin()
    {
        Name = "Test";
        Description = "This is a test plugin for plugin usage sample";
    }

    public override Task Initialize()
    {
        AddRequestHandler(new PluginRequestHandler());
        return Task.CompletedTask;
    }
}