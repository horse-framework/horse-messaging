using Twino.Server;
using Twino.Server.Http;
using System;
using System.Net;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Sample.Http.Server
{
    public class RequestHandler : IHttpRequestHandler
    {
        public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            response.SetToHtml();
            Console.WriteLine("Hello World: " + request.Content);
        }

        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            await Task.Factory.StartNew(() => Request(server, request, response));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RequestHandler handler = new RequestHandler();
            TwinoServer server = TwinoServer.CreateHttp(handler);
            server.Start();

            Console.ReadLine();

            string request = "GET /test-demo HTTP/1.1" + Environment.NewLine + "Content-Length: 16" + Environment.NewLine + "Host: 127.0.0.1" + Environment.NewLine + Environment.NewLine + "isim=mehmet&no=1";
            string s = request.Substring(0, 61);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(request);

            while (true)
            {
                for (int i = 0; i < 100; i++)
                {
                    /*
                    TcpClient client = new TcpClient();
                    client.Connect("127.0.0.1", 80);
                    NetworkStream ns = client.GetStream();
                    ns.Write(bytes, 0, bytes.Length);
                    */


                    WebClient wc = new WebClient();
                    string data = wc.DownloadString("http://127.0.0.1/test-demo");
                    Console.WriteLine(data);
                }

                Console.ReadLine();

                Console.WriteLine(new string('-', 50));
            }
        }

        private static void Wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            Console.WriteLine("done");
        }
    }
}