using Horse.Messaging.Protocol;

namespace AdvancedSample.Server.Models
{
    public class RouterConfig
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public RouteMethod Method { get; set; }
        public List<BindingConfig> Bindings { get; set; }
    }

    public class BindingConfig
    {
        public RouteMethod RouteMethod { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public string Type { get; set; }
        public int Priority { get; set; }
        public BindingInteraction Interaction { get; set; }
        public ushort? ContentType { get; internal set; }
    }
}
