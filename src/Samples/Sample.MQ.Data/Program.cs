using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;

namespace Sample.MQ.Data
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Overload o = new Overload();
            await o.OverloadAsync();
        }
        
    }
}