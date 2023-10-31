namespace AdvancedSample.Server.Models
{
    public class ChennelsOptionsConfig
    {
        public bool AutoChannelCreation { get; set; } = true;
        public bool AutoDestroy { get; set; } = true;
        public int ClientLimit { get; set; } = 0;
        public ulong MessageSizeLimit { get; set; } = 0;
    }
}
