namespace AdvancedSample.Server.Models;

public class CacheConfig
{
    public int MaximumKeys { get; set; }
    public int ValueMaxSize { get; set; }
    public string DefaultDuration { get; set; }
    public string MaximumDuration { get; set; }
}