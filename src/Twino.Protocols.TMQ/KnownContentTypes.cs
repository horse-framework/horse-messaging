
namespace Twino.Protocols.TMQ
{
    public class KnownContentTypes
    {
        public const ushort Hello = 101;
        public const ushort Ok = 200;
        public const ushort Accepted = 202;
        public const ushort BadRequest = 400;
        public const ushort Unauthorized = 401;
        public const ushort NotFound = 404;
        public const ushort Unacceptable = 406;
        public const ushort Duplicate = 481;
        public const ushort Failed = 500;
        public const ushort Busy = 503;

        public const ushort Join = 601;
        public const ushort Leave = 602;

        public const ushort CreateQueue = 610;
    }
}