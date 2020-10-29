using Twino.Client.TMQ.Annotations;

namespace Test.Bus.Models
{
    [RouterName("test-router")]
    public class RouterModel
    {
        public string Foo { get; set; }
    }
}