using EnumsNET;

namespace Horse.Messaging.Server.Routing;

public class BindingConfiguration
{
    public string Name { get; set; }
    public string Type { get; set; }

    /// <summary>
    /// Name of the assembly of the binding type, without version.
    /// Type is the full name only, which finds the types of the server assembly; a custom binding type
    /// in another assembly needs its assembly to be found. Null in configurations saved by older versions.
    /// </summary>
    public string AssemblyName { get; set; }

    public string Target { get; set; }
    public ushort? ContentType { get; set; }
    public int Priority { get; set; }
    public string Method { get; set; }
    public string Interaction { get; set; }

    public static BindingConfiguration Create(Binding binding)
    {
        System.Type type = binding.GetType();

        return new BindingConfiguration
        {
            Name = binding.Name,
            Interaction = binding.Interaction.AsString(EnumFormat.Description),
            Target = binding.Target,
            ContentType = binding.ContentType,
            Priority = binding.Priority,
            Type = type.FullName,
            AssemblyName = type.Assembly.GetName().Name,
            Method = binding.RouteMethod.AsString(EnumFormat.Description)
        };
    }
}