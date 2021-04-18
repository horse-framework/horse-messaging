using Horse.Messaging.Client.Annotations;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [RouterName("test-router")]
    public class RouterModel
    {
        public string Foo { get; set; }
    }
}