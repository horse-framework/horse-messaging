using System;
using System.Threading.Tasks;
using Test.Bus.Consumers;
using Test.Bus.Models;
using Test.Common;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Bus
{
    public class QueueAttributesTest
    {
        [Fact]
        public async Task AutoAckWithDelay()
        {
            Registrar registrar = new Registrar();
            TestTwinoMQ server = new TestTwinoMQ();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            TmqStickyConnector producer = new TmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("tmq://localhost:" + port);
            producer.Run();
            
            TmqStickyConnector consumer = new TmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("tmq://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model1 model = new Model1();
            TwinoResult push = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Ok, push.Code);
            Assert.Equal(1, QueueConsumer1.Instance.Count);
        }
        
        [Fact]
        public async Task AutoNackWithPutBackDelay()
        {
            Registrar registrar = new Registrar();
            TestTwinoMQ server = new TestTwinoMQ();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            TmqStickyConnector producer = new TmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("tmq://localhost:" + port);
            producer.Run();
            
            TmqStickyConnector consumer = new TmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("tmq://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model2 model = new Model2();
            TwinoResult push = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push.Code);
            Assert.Equal(1, QueueConsumer2.Instance.Count);
        }
        
        [Fact]
        public void PushExceptionsWithTopic()
        {
            throw new NotImplementedException();
        }
        
        [Fact]
        public void PublishExceptionsWithHighPriority()
        {
            throw new NotImplementedException();
        }
        
        [Fact]
        public void RetryWaitForAcknowledge()
        {
            throw new NotImplementedException();
        }
    }
}