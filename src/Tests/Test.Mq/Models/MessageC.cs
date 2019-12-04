using Test.Mq.Internal;

namespace Test.Mq.Models
{
    public class MessageC
    {
        public static readonly ushort ContentType = 1200;

        public MessageA A { get; set; }

        public string Name { get; set; }

        public int Number { get; set; }

        public MessageC(string name, string username)
        {
            Name = name;
            Number = Helpers.GetRandom();
            A = new MessageA(username);
        }
    }
}