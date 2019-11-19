namespace Twino.MQ.Models
{
    public class ClientInformation
    {
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Token { get; set; }
        
        public string[] Channels { get; set; }
    }
}