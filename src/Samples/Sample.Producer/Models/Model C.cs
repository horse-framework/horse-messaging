using Twino.Client.TMQ.Annotations;

namespace Sample.Producer.Models
{
    [ContentType(5)]
    [RouterName("deneme-router")]
    public class ModelC
    {
        public string Value { get; set; }
    }
}