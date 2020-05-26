using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.MQ.Routing;
using Xunit;

namespace Test.Mq
{
    /// <summary>
    /// Ports 42900 - 42950
    /// </summary>
    public class RouterTest
    {
        [Fact]
        public async Task Distribute()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task RoundRobin()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task OnlyFirst()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleQueue()
        {
            int port = 47411;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "ch-push-cc", MessageA.ContentType, 0, BindingInteraction.None));
            
            server.Server.AddRouter(router);


            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleDirect()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleOfflineDirect()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task SingleQueueSingleDirect()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleQueueMultipleDirect()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleQueueMultipleDirectAckFromQueue()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleQueueMultipleDirectResponseFromDirect()
        {
            throw new NotImplementedException();
        }
    }
}