using System.Collections.Generic;
using EnumsNET;

namespace Horse.Messaging.Server.Routing;

public class RouterConfiguration
{
    public string Name { get; set; }
    public bool IsEnabled { get; set; }
    public string Method { get; set; }
    public List<BindingConfiguration> Bindings { get; set; } = new();

    public static RouterConfiguration Create(Router router)
    {
        RouterConfiguration configuration = new RouterConfiguration
        {
            Name = router.Name,
            Method = router.Method.AsString(EnumFormat.Description),
            IsEnabled = router.IsEnabled,
            Bindings = new List<BindingConfiguration>()
        };

        foreach (Binding binding in router.GetBindings())
            configuration.Bindings.Add(BindingConfiguration.Create(binding));

        return configuration;
    }
}