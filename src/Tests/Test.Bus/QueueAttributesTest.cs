using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Routing;
using Test.Bus.Consumers;
using Test.Bus.Models;
using Test.Common;
using Horse.Mq.Client.Connectors;
using Xunit;

namespace Test.Bus
{
    public class QueueAttributesTest
    {
        [Fact]
        public async Task AutoAckWithDelay()
        {
            Registrar registrar = new Registrar();
            TestHorseMq server = new TestHorseMq();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            HmqStickyConnector producer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("horse://localhost:" + port);
            producer.Run();
            
            HmqStickyConnector consumer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("horse://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model1 model = new Model1();
            HorseResult push = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Ok, push.Code);
            Assert.Equal(1, QueueConsumer1.Instance.Count);
        }
        
        [Fact]
        public async Task AutoNackWithPutBackDelay()
        {
            Registrar registrar = new Registrar();
            TestHorseMq server = new TestHorseMq();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            HmqStickyConnector producer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("horse://localhost:" + port);
            producer.Run();
            
            HmqStickyConnector consumer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("horse://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model2 model = new Model2();
            HorseResult push = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push.Code);
            Assert.Equal(1, QueueConsumer2.Instance.Count);
        }
        
        [Fact]
        public async Task PushExceptionsWithTopic()
        {
            Registrar registrar = new Registrar();
            TestHorseMq server = new TestHorseMq();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            HmqStickyConnector producer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("horse://localhost:" + port);
            producer.Run();
            
            HmqStickyConnector consumer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("horse://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model3 model = new Model3();
            
            HorseResult push1 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push1.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(0, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            HorseResult push2 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push2.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            HorseResult push3 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push3.Code);
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
            TestHorseMq server = new TestHorseMq();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            IRouter router1 = server.Server.AddRouter("ex-route-1", RouteMethod.Distribute);
            IRouter router2 = server.Server.AddRouter("ex-route-2", RouteMethod.Distribute);
            IRouter router3 = server.Server.AddRouter("ex-route-3", RouteMethod.Distribute);
            
            router1.AddBinding(new QueueBinding("bind-1", "ex-queue-1", 0, BindingInteraction.None));
            router2.AddBinding(new QueueBinding("bind-2", "ex-queue-2", 0, BindingInteraction.None));
            router3.AddBinding(new QueueBinding("bind-3", "ex-queue-3", 0, BindingInteraction.None));

            HmqStickyConnector producer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("horse://localhost:" + port);
            producer.Run();
            
            HmqStickyConnector consumer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("horse://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model4 model = new Model4();
            
            HorseResult push1 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push1.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(0, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);

            HorseResult push2 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push2.Code);
            await Task.Delay(100);
            
            Assert.Equal(1, ExceptionConsumer1.Instance.Count);
            Assert.Equal(1, ExceptionConsumer2.Instance.Count);
            Assert.Equal(0, ExceptionConsumer3.Instance.Count);
            
            HorseResult push3 = await producer.Bus.Queue.PushJson(model, true);
            Assert.Equal(HorseResultCode.Failed, push3.Code);
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
            TestHorseMq server = new TestHorseMq();
            server.SendAcknowledgeFromMQ = false;
            await server.Initialize();
            int port = server.Start(300, 300);

            HmqStickyConnector producer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            producer.AddHost("horse://localhost:" + port);
            producer.Run();
            
            HmqStickyConnector consumer = new HmqAbsoluteConnector(TimeSpan.FromSeconds(10));
            consumer.AddHost("horse://localhost:" + port);
            registrar.Register(consumer);
            consumer.Run();
            
            await Task.Delay(500);
            Assert.True(producer.IsConnected);
            Assert.True(consumer.IsConnected);

            Model5 model = new Model5();
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            HorseResult push = await producer.Bus.Queue.PushJson(model, true);
            sw.Stop();

            Assert.Equal(HorseResultCode.Failed, push.Code);
            Assert.Equal(5, QueueConsumer5.Instance.Count);
            
            //5 times with 50 ms delay between them (5 tries, 4 delays)
            Assert.True(sw.ElapsedMilliseconds > 190);
        }
    }
}