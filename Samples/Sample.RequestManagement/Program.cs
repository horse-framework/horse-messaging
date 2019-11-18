using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Server;
using Twino.SocketModels;
using Twino.SocketModels.Models;
using Sample.RequestManagement.Models;
using Twino.Client;
using Twino.Core.Http;
using Twino.SocketModels.Requests;

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

        private static TwinoMvc _mvc;
        private static TaskCompletionSource<DemoResponseModel> source;

        static void FuncFromSomewhere(DemoRequestModel request)
        {
            Thread.Sleep(4000);
            Console.WriteLine($"Request: {request.Text} > {request.Number}");

            DemoResponseModel response = new DemoResponseModel();
            response.Id = 1;
            response.ResultCode = 200;
            response.Message = "Success";

            Thread.Sleep(4000);
            Console.WriteLine("Setting result");
            source.SetResult(response);
        }

        static void StartServer()
        {
            R = new RequestManager();
            R.OnScheduled<DemoRequestModel, DemoResponseModel>(request =>
            {
                Console.WriteLine("Request received");
                source = new TaskCompletionSource<DemoResponseModel>(TaskCreationOptions.None);
                ThreadPool.QueueUserWorkItem(FuncFromSomewhere, request, false);
                return source.Task;
            });

            _mvc = new TwinoMvc(new CF());
            _mvc.Init();
            _mvc.RunAsync();
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
                var task = client.Request<DemoResponseModel>(request, 25000);

                SocketResponse<DemoResponseModel> response = task.Result;
                sw.Stop();

                Console.WriteLine(response.Status + " > " + response.Unique + " in " + sw.ElapsedMilliseconds);
                while (true)
                {
                    Console.Write("running...");
                    string q = Console.ReadLine();
                    if (q == "q")
                        break;
                }
            }
        }
    }
}