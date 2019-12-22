using System;
using System.Threading.Tasks;
using Twino.MQ.Queues;
using Xunit;

namespace Test.Mq
{
    public class ChangeStatusTest
    {
        [Theory]
        [InlineData(QueueStatus.Route, QueueStatus.Stopped, QueueStatus.Route)]
        [InlineData(QueueStatus.Route, QueueStatus.Stopped, QueueStatus.Push)]
        [InlineData(QueueStatus.Route, QueueStatus.Stopped, QueueStatus.Pull)]
        [InlineData(QueueStatus.Route, QueueStatus.Stopped, QueueStatus.RoundRobin)]
        [InlineData(QueueStatus.Route, QueueStatus.Stopped, QueueStatus.Paused)]
        [InlineData(QueueStatus.Push, QueueStatus.Stopped, QueueStatus.Route)]
        [InlineData(QueueStatus.Push, QueueStatus.Stopped, QueueStatus.Push)]
        [InlineData(QueueStatus.Push, QueueStatus.Stopped, QueueStatus.Pull)]
        [InlineData(QueueStatus.Push, QueueStatus.Stopped, QueueStatus.RoundRobin)]
        [InlineData(QueueStatus.Push, QueueStatus.Stopped, QueueStatus.Paused)]
        [InlineData(QueueStatus.Pull, QueueStatus.Stopped, QueueStatus.Route)]
        [InlineData(QueueStatus.Pull, QueueStatus.Stopped, QueueStatus.Push)]
        [InlineData(QueueStatus.Pull, QueueStatus.Stopped, QueueStatus.Pull)]
        [InlineData(QueueStatus.Pull, QueueStatus.Stopped, QueueStatus.RoundRobin)]
        [InlineData(QueueStatus.Pull, QueueStatus.Stopped, QueueStatus.Paused)]
        [InlineData(QueueStatus.RoundRobin, QueueStatus.Stopped, QueueStatus.Route)]
        [InlineData(QueueStatus.RoundRobin, QueueStatus.Stopped, QueueStatus.Push)]
        [InlineData(QueueStatus.RoundRobin, QueueStatus.Stopped, QueueStatus.Pull)]
        [InlineData(QueueStatus.RoundRobin, QueueStatus.Stopped, QueueStatus.RoundRobin)]
        [InlineData(QueueStatus.RoundRobin, QueueStatus.Stopped, QueueStatus.Paused)]
        public async Task ChangeStatus(QueueStatus first, QueueStatus second, QueueStatus third)
        {
            int port = 48000 + (Convert.ToInt32(first) * 100) + (Convert.ToInt32(second) * 10) + Convert.ToInt32(third);

            throw new NotImplementedException();
        }
    }
}