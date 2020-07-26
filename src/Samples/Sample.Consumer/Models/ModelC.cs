using Twino.Client.TMQ.Annotations;

namespace Sample.Consumer.Models
{
    [ContentType(5)]
    [DirectTarget(FindTargetBy.Name, "target-name")]
    public class ModelC
    {
        public string Value { get; set; }
    }
}