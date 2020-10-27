using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Test.Bus.Consumers;
using Test.Bus.Models;
using Test.Common;
using Twino.Client.TMQ.Connectors;
using Twino.MQ.Routing;
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
        public async Task PushExceptionsWithTopic()
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

            Model3 model = new Model3();
            
            TwinoResult push1 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push1.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(0, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            TwinoResult push2 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push2.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            TwinoResult push3 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push3.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(1, ExceptionConsumer3.Instance.Count);
            
            Assert.Equal(3, QueueConsumer3.Instance.Count);
        }
        
        [Fact]
        public async Task PublishExceptionsWithHighPriority()
        {
            Registrar registrar = new Registrar();
            TestTwinoMQ server = new TestTwinoMQ();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            IRouter router1 = server.Server.AddRouter("ex-route-1", RouteMethod.Distribute);
            IRouter router2 = server.Server.AddRouter("ex-route-2", RouteMethod.Distribute);
            IRouter router3 = server.Server.AddRouter("ex-route-3", RouteMethod.Distribute);
            
            router1.AddBinding(new QueueBinding("bind-1", "ex-queue-1", 0, BindingInteraction.None));
            router2.AddBinding(new QueueBinding("bind-2", "ex-queue-2", 0, BindingInteraction.None));
            router3.AddBinding(new QueueBinding("bind-3", "ex-queue-3", 0, BindingInteraction.None));

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

            Model4 model = new Model4();
            
            TwinoResult push1 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push1.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(0, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);

            TwinoResult push2 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push2.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            TwinoResult push3 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(TwinoResultCode.Failed, push3.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(1, ExceptionConsumer3.Instance.Count);
            
            Assert.Equal(3, QueueConsumer4.Instance.Count);
        }
        
        [Fact]
        public async Task RetryWaitForAcknowledge()
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

            Model5 model = new Model5();
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            TwinoResult push = await producer.Bus.Queue.PushJson(model, true);
            sw.Stop();

            Assert.Equal(TwinoResultCode.Failed, push.Code);
            Assert.Equal(5, QueueConsumer5.Instance.Count);
            
            //5 times with 50 ms delay between them (5 tries, 4 delays)
            Assert.True(sw.ElapsedMilliseconds > 190);
        }
    }
}