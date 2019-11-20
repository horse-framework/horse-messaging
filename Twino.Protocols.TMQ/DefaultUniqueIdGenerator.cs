using System;

namespace Twino.Protocols.TMQ
{
    public class DefaultUniqueIdGenerator : IUniqueIdGenerator
    {
        public string Create()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}