namespace Test.Mq.Models
{
    public class MessageB
    {
        public static readonly ushort ContentType = 1002;
        
        public string Category { get; set; }

        public MessageB()
        {
        }

        public MessageB(string category)
        {
            Category = category;
        }
    }
}