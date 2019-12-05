using System;
using System.Threading;
using System.Threading.Tasks;
using Test.SocketModels.Helpers;
using Test.SocketModels.Models;
using Twino.Client.WebSocket;
using Twino.Protocols.WebSocket.Requests;
using Xunit;

namespace Test.SocketModels
{
    public class RequestResponse
    {
        [Fact]
        public void ResponseSuccess()
        {
            TestServer server = new TestServer(311, 100);
            server.Run();
            Thread.Sleep(250);

            TwinoWebSocket client = new TwinoWebSocket();
            client.Connect("127.0.0.1", 311, false);
            RequestModel model = new RequestModel();
            model.Delay = 100;
            model.Value = Guid.NewGuid().ToString();

            SocketResponse<ResponseModel> response = client.Request<ResponseModel>(model, 500).Result;

            Assert.Equal(ResponseStatus.Success, response.Status);
            Assert.Equal(response.Model.Value, model.Value);
            client.Disconnect();
        }

        [Fact]
        public void ResponseFail()
        {
            TestServer server = new TestServer(382, 100);
            server.Run();
            Thread.Sleep(250);

            TwinoWebSocket client = new TwinoWebSocket();
            client.Connect("127.0.0.1", 382, false);
            RequestModel model = new RequestModel();
            model.Delay = 100;
            model.Value = "FAIL";

            SocketResponse<ResponseModel> response = client.Request<ResponseModel>(model, 50).Result;

            Assert.Equal(ResponseStatus.Failed, response.Status);
            client.Disconnect();
        }

        [Fact]
        public void ResponseTimeout()
        {
            RequestManager requestManager = new RequestManager();
            TestServer server = new TestServer(313, 100);
            server.Run();
            Thread.Sleep(250);

            TwinoWebSocket client = new TwinoWebSocket();
            client.Connect("127.0.0.1", 313, false);
            RequestModel model = new RequestModel();
            model.Delay = 20000;
            model.Value = Guid.NewGuid().ToString();

            SocketResponse<ResponseModel> response = client.Request<ResponseModel>(model, 3).Result;

            Assert.Equal(ResponseStatus.Timeout, response.Status);
            client.Disconnect();
        }

        [Fact]
        public void ResponseError()
        {
            RequestManager requestManager = new RequestManager();
            TestServer server = new TestServer(314, 100);
            server.Run();
            Thread.Sleep(250);

            TwinoWebSocket client = new TwinoWebSocket();
            client.Connect("127.0.0.1", 314, false);
            RequestModel model = new RequestModel();
            model.Delay = 8000;
            model.Value = Guid.NewGuid().ToString();

            Task<SocketResponse<ResponseModel>> task = client.Request<ResponseModel>(model, 10);
            Thread.Sleep(500);
            client.Disconnect();

            SocketResponse<ResponseModel> response = task.Result;
            Assert.Equal(ResponseStatus.ConnectionError, response.Status);
        }

        [Theory]
        [InlineData(10, 1, 321)]
        [InlineData(10, -200, 322)]
        [InlineData(10, -2000, 323)]
        public void HighTraffic(int concurrentClients, int requestDelay, int port)
        {
            TestServer server = new TestServer(port, requestDelay);
            server.Run();
            Thread.Sleep(250);

            int completed = 0;

            for (int i = 0; i < concurrentClients; i++)
            {
                ThreadPool.QueueUserWorkItem(async s =>
                {
                    await Task.Yield();
                    TwinoWebSocket client = new TwinoWebSocket();
                    client.Connect("127.0.0.1", port, false);
                    RequestModel model = new RequestModel();
                    model.Delay = requestDelay;
                    model.Value = Guid.NewGuid().ToString();

                    SocketResponse<ResponseModel> response = await client.Request<ResponseModel>(model, 100);

                    Assert.Equal(ResponseStatus.Success, response.Status);
                    Assert.Equal(response.Model.Value, model.Value);

                    completed++;
                    client.Disconnect();
                });
            }

            Thread.Sleep(8000);
            Assert.Equal(concurrentClients, completed);
        }
    }
}