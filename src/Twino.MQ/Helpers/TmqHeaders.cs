namespace Twino.MQ.Helpers
{
    public class TmqHeaders
    {
        public static readonly string CLIENT_ID = "Client-Id";
        public static readonly string CLIENT_TOKEN = "Client-Token";
        public static readonly string CLIENT_NAME = "Client-Name";
        public static readonly string CLIENT_TYPE = "Client-Type";
        public static readonly string CLIENT_ACCEPT = "Client-Accept";

        public static readonly string VALUE_ACCEPTED = "Accepted";
        public static readonly string VALUE_UNAUTHORIZED = "Unauthorized";
        public static readonly string VALUE_BUSY = "Busy";

        public static readonly string SOURCE_TARGET_SERVER = "SERVER";

        public static readonly ushort HEADER_CONTENT_TYPE = 101;
    }
}