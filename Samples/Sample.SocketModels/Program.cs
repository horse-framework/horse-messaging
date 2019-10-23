using Twino.Server;
using Twino.SocketModels;
using System;
using Twino.Client;
using Twino.SocketModels.Serialization;

namespace Sample.SocketModels
{
    public class LoginModel : ISocketModel
    {
        public int Type { get; set; } = KnownModelTypes.Login;

        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResultModel : ISocketModel
    {
        public int Type { get; set; } = 200;

        public bool Success { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            InitServer();
            System.Threading.Thread.Sleep(1500);

            InitClient();
            Console.ReadLine();
        }

        static void InitServer()
        {
            PackageReader reader = new PackageReader();

            reader.On<LoginModel>((socket, message) =>
            {
                Console.WriteLine($"login received {message.Username} {message.Password}");
                LoginResultModel result = new LoginResultModel();
                result.Success = true;
                socket.Send(result, new TwinoModelWriter());
            });

            TwinoServer twino = TwinoServer.CreateWebSocket().UsePackageReader(reader);

            twino.Start(85);
        }

        static void InitClient()
        {
            PackageReader pm = new PackageReader();

            pm.On<LoginResultModel>((client, model) => { Console.WriteLine($"# Login Result: {model.Success}"); });

            TwinoClient clientSocket = new TwinoClient();

            clientSocket.Connect("127.0.0.1", 85, false);

            LoginModel login = new LoginModel
                               {
                                   Username = "mehmet",
                                   Password = "V3ry!S3cre7"
                               };

            clientSocket.Send(login, new TwinoModelWriter());
        }
    }
}