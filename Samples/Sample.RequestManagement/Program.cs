using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Server;
using Twino.Server.WebSockets;
using Twino.SocketModels;
using Twino.SocketModels.Models;
using Sample.RequestManagement.Models;
using Twino.Client;
using Twino.Core.Http;

namespace Sample.RequestManagement
{
    public class CF : IClientFactory
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            ServerSocket socket = new ServerSocket(server, request, client);
            socket.MessageReceived += (s, m) => { Program.R.HandleRequests(s, m); };
            return await Task.FromResult(socket);
        }
    }

    class Program
    {
        public static RequestManager R { get; private set; }

        static void Main(string[] args)
        {
            StartServer();
            StartClient();
        }

        static void StartServer()
        {
            R = new RequestManager();
            R.On<DemoRequestModel, DemoResponseModel>(request =>
            {
                Console.WriteLine($"Request: {request.Text} > {request.Number}");

                DemoResponseModel response = new DemoResponseModel();
                response.Id = 1;
                response.ResultCode = 200;
                response.Message = "Success";

                return response;
            });

            using (TwinoMvc mvc = new TwinoMvc(new CF()))
            {
                mvc.Init();
                mvc.RunAsync();
            }
        }

        static void StartClient()
        {
            TwinoClient client = new TwinoClient();
            client.Connect("ws://127.0.0.1:84");

            int x = 1234;
            while (true)
            {
                DemoRequestModel request = new DemoRequestModel
                                           {
                                               Text = "Test Request",
                                               Number = ++x
                                           };


                Stopwatch sw = new Stopwatch();
                sw.Start();
                var task = R.Request<DemoResponseModel>(client, request, 500);

                SocketResponse<DemoResponseModel> response = task.Result;
                sw.Stop();

                Console.WriteLine(response.Status + " > " + response.Unique + " in " + sw.ElapsedMilliseconds);
                Console.ReadLine();
            }
        }
    }
}