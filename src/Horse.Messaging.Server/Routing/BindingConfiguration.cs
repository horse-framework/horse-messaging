using EnumsNET;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Routing
{
    public class BindingConfiguration
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Target { get; set; }
        public ushort? ContentType { get; set; }
        public int Priority { get; set; }
        public string Method { get; set; }
        public string Interaction { get; set; }

        public static BindingConfiguration Create(Binding binding)
        {
            return new BindingConfiguration
            {
                Name = binding.Name,
                Interaction = binding.Interaction.AsString(EnumFormat.Description),
                Target = binding.Target,
                ContentType = binding.ContentType,
                Priority = binding.Priority,
                Type = binding.GetType().FullName,
                Method = binding.RouteMethod.AsString(EnumFormat.Description)
            };
        }
    }
}