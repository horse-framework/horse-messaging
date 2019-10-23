using Twino.SocketModels;

namespace Test.SocketModels.Models
{
    public class ResponseModel : ISocketModel
    {
        public int Type { get; set; } = 101;
        public int Delay { get; set; }
        
        public string Value { get; set; }
    }
}