using Twino.MQ.Client.Annotations;

namespace Test.Bus.Models
{
    [RouterName("test-router")]
    public class RouterModel
    {
        public string Foo { get; set; }
    }
}