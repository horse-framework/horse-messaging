namespace Twino.MQ.Data
{
    public enum DataType : byte
    {
        Empty = 0x00,
        Insert = 0x10,
        Delete = 0x11
    }
}