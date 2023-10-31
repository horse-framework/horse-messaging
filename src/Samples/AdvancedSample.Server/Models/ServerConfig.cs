using Horse.Server;
using HostOptions = Horse.Server.HostOptions;

namespace AdvancedSample.Server.Models
{
    public class ServerConfig
    {
        public string ServerName { get; set; }
        public int PingInterval { get; set; }
        public int RequestTimeout { get; set; }
        public string ContentEncoding { get; set; }
        public int MaximumPendingConnections { get; set; }
        public List<HostOptions> Hosts { get; set; }
    }
}
