using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horse.Messaging.Data")]

namespace Horse.Messaging.Server;

/// <summary>
/// Horse MQ Builder
/// </summary>
public class HorseRiderBuilder
{
    internal HorseRider Rider { get; set; }

    /// <summary>
    /// Creates new rider builder
    /// </summary>
    public HorseRiderBuilder()
    {
        Rider = new HorseRider();
    }

    /// <summary>
    /// Creates new Horse Rider Builder
    /// </summary>
    public static HorseRiderBuilder Create()
    {
        return new HorseRiderBuilder();
    }

    /// <summary>
    /// Gets Horse Rider Object
    /// </summary>
    public HorseRider Build()
    {
        Rider.Initialize();
        return Rider;
    }
}