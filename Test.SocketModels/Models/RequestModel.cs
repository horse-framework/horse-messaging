using Twino.SocketModels;

namespace Test.SocketModels.Models
{
    public class RequestModel : ISocketModel
    {
        public int Type { get; set; } = 100;
        public int Delay { get; set; }
        
        public string Value { get; set; }
    }
}