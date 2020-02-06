using System;

namespace Twino.MQ.Data
{
    public class DatabaseOptions
    {
        public bool AutoShrink { get; set; }
        public TimeSpan ShrinkInterval { get; set; }
        
        public bool InstantFlush { get; set; }
        public bool AutoFlush { get; set; }
        public TimeSpan FlushInterval { get; set; }
    }
}