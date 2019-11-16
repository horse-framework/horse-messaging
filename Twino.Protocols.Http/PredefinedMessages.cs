namespace Twino.Protocols.Http
{
    internal static class PredefinedMessages
    {
        public const byte CR = (byte) '\r';
        public const byte LF = (byte) '\n';
        public const byte COLON = (byte) ':';
        public const byte SEMICOLON = (byte) ';';
        public const byte QMARK = (byte) '?';
        public const byte AND = (byte) '&';
        public const byte EQUALS = (byte) '=';
        public const byte SPACE = (byte) ' ';
        public static readonly byte[] CRLF = {CR, LF};
        public static readonly byte[] COLON_SPACE = {COLON, SPACE};
    }
}