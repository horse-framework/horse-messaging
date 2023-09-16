namespace HorseService;

public class AppOptions
{
    public int Port { get; set; } = 2626;
    public int JockeyPort { get; set; } = 2627;

    public string JockeyUsername { get; set; } = "";
    public string JockeyPassword { get; set; } = "";

    public string DataPath { get; set; } = "/etc/horse/data";
}