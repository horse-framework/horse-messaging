namespace HorseService;

public class AppOptions
{
    public int Port { get; set; } = 34000;
    public int JockeyPort { get; set; } = 34001;

    public string JockeyUsername { get; set; } = "admin";
    public string JockeyPassword { get; set; } = "";

    public string DataPath { get; set; } = "/etc/horse/data";
}