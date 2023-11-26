namespace AdvancedSample.Core;

public class HorseOptions
{
    public string Host { get; set; }
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string Protocol => $"hmq{(UseSsl ? "s" : string.Empty)}";

    public override string ToString()
    {
        return $"{Protocol}://{Host}:{Port}";
    }
}