namespace AdvancedSample.Server.Models
{
    public class LogConfig
    {
        public string LogFileAddressDirectory { get; set; }
        public string LogFileName { get; set; }
        public int FileSizeLimit { get; set; } = 10000000;
    }
}
