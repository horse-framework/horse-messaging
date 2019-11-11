using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.Server;
using Twino.Server.WebSockets;
using Test.SocketModels.Models;
using Twino.SocketModels.Requests;

namespace Test.SocketModels.Helpers
{
    public class TestServer
    {
        private readonly int _port;
        private readonly int _requestDelay;

        public TwinoServer Server { get; private set; }
        private RequestManager _requestManager;
        private readonly Random _rnd = new Random();

        public TestServer(int port, int requestDelay)
        {
            _port = port;
            _requestDelay = requestDelay;
        }

        public void Run()
        {
            ServerOptions options = ServerOptions.CreateDefault();
            options.Hosts.FirstOrDefault().Port = _port;

            _requestManager = new RequestManager();
            _requestManager.On<RequestModel, ResponseModel>(request =>
            {
                if (request.Value.Contains("FAIL"))
                    throw new InvalidOperationException();

                if (request.Delay < 0)
                {
                    int delay = _rnd.Next(0, request.Delay * -1);
                    System.Threading.Thread.Sleep(delay);
                }
                else
                    System.Threading.Thread.Sleep(request.Delay);

                ResponseModel response = new ResponseModel();
                response.Delay = request.Delay;
                response.Value = request.Value;

                return response;
            });

            Server = TwinoServer.CreateWebSocket(async (server, request, client) =>
            {
                ServerSocket socket = new ServerSocket(server, request, client);
                socket.MessageReceived += async (s, m) => await _requestManager.HandleRequests(s, m);
                return await Task.FromResult(socket);
            }, options);

            Server.Start(_port);
        }
    }
}