using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [DirectContentType(222)]
    [DirectTarget(FindTargetBy.Name, "direct-receiver")]
    public class DirectModel
    {
        public string Foo { get; set; }
    }
}