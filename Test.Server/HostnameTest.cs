using System;
using Twino.Core;
using Xunit;

namespace Test.Server
{
    public class HostnameTest
    {

        [Theory]
        [InlineData("http://github.com")]
        [InlineData("http://github.com/mhelvacikoylu/twino")]
        [InlineData("https://github.com/mhelvacikoylu/twino")]
        [InlineData("HTtPs://github.com/mhelvacikoylu/twino")]
        [InlineData("HTtP://github.com/")]
        [InlineData("www.github.com")]
        [InlineData("www.github.com:82")]
        [InlineData("https://www.github.com:82")]
        [InlineData("www.github.com/mhelvacikoylu/twino")]
        [InlineData("www.github.com:82/mhelvacikoylu/twino")]
        [InlineData("wss://www.github.com:82/mhelvacikoylu/twino")]
        [InlineData("ws://github.com")]
        [InlineData("ws://github.com/mhelvacikoylu/twino")]
        [InlineData("wss://github.com/mhelvacikoylu/twino")]
        [InlineData("wSs://github.com/mhelvacikoylu/twino")]
        [InlineData("wS://github.com/")]
        [InlineData("ws://www.github.com:82")]
        [InlineData("ws://www.github.com:82/mhelvacikoylu/twino")]
        public void CheckHostnames(string hostname)
        {
            DnsResolver resolver = new DnsResolver();
            DnsInfo info = resolver.Resolve(hostname);

            Assert.NotNull(info.IPAddress);
            Assert.NotNull(info.Path);
            Assert.True(info.Port > 0);

            int i = hostname.IndexOf("://", StringComparison.Ordinal);
            if (i > 0)
            {
                string protocol = hostname.Substring(0, i).ToUpper();
                switch(protocol)
                {
                    case "WS":
                        Assert.False(info.SSL);
                        Assert.True(info.Protocol == Protocol.WebSocket);
                        break;

                    case "WSS":
                        Assert.True(info.SSL);
                        Assert.True(info.Protocol == Protocol.WebSocket);
                        break;

                    case "HTTP":
                        Assert.False(info.SSL);
                        Assert.True(info.Protocol == Protocol.Http);
                        break;

                    case "HTTPS":
                        Assert.True(info.SSL);
                        Assert.True(info.Protocol == Protocol.Http);
                        break;
                }
            }

        }

    }
}
