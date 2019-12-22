using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class PullStatusTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public async Task SendToOnlineConsumers(int onlineConsumerCount)
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task SendToOfflineConsumers()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task RequestAcknowledge()
        {
            throw new NotImplementedException();
        }
    }
}