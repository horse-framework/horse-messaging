using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Routing
{
    internal class BindingConfigData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Target { get; set; }
        public ushort? ContentType { get; set; }
        public int Priority { get; set; }
        public RouteMethod? Method { get; set; }
        public BindingInteraction Interaction { get; set; }
    }
}