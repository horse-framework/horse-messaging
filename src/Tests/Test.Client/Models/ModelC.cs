using Horse.Messaging.Client.Direct.Annotations;

namespace Test.Client.Models
{
    [DirectContentType(250)]
    [DirectTarget(FindTargetBy.Name,"test-client")]
    public class ModelC
    {
        public string Foo { get; set; }
    }
}