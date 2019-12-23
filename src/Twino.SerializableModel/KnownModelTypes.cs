namespace Twino.SerializableModel
{
    /// <summary>
    /// Must used model types for general purpose
    /// </summary>
    public static class KnownModelTypes
    {
        public const int Login = 9001;
        public const int Logout = 9002;
        public const int Logoff = 9003;
        public const int Lock = 9004;
        public const int Unlock = 9005;

        public const int Join = 9101;
        public const int Leave = 9102;
        public const int Invite = 9103;
        public const int Kick = 9104;
        public const int Ban = 9105;

        public const int Subscribe = 9111;
        public const int Unsubscribe = 9112;

        public const int Shutdown = 9201;
        public const int Restart = 9202;
        public const int Log = 9203;

        public const int Sleep = 9211;
        public const int WakeUp = 9212;

        public const int Start = 9221;
        public const int Stop = 9222;
        public const int Pause = 9223;
        public const int Resume = 9224;
        public const int Play = 9225;

        public const int Begin = 9231;
        public const int End = 9232;

        public const int Error = 9901;
    }
}