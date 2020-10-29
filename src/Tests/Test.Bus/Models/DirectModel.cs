using Twino.Client.TMQ.Annotations;

namespace Test.Bus.Models
{
    [ContentType(222)]
    [DirectTarget(FindTargetBy.Name, "direct-receiver")]
    public class DirectModel
    {
        public string Foo { get; set; }
    }
}