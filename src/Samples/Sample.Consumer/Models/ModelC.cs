using Twino.Client.TMQ.Annotations;

namespace Sample.Consumer.Models
{
    [ContentType(5)]
    public class ModelC
    {
        public string Value { get; set; }
    }
}