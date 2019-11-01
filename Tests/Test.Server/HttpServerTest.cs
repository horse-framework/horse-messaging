using Twino.Server;
using Twino.Server.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Twino.Core.Http;
using Xunit;

namespace Test.Server
{
    public class HttpServerTest
    {
        private class RequestHandler : IHttpRequestHandler
        {
            public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
            {
                response.StatusCode = HttpStatusCode.OK;

                Assert.NotNull(server);
                Assert.NotNull(request);
                Assert.NotNull(request.IpAddress);
                Assert.NotNull(request.Response);
                Assert.NotNull(response);
                Assert.False(request.IsWebSocket);

                if (request.Method == "POST")
                {
                    Assert.NotNull(request.ContentStream);
                    Assert.True(request.ContentStream.Length > 0);
                }
            }

            public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
            {
                await Task.Factory.StartNew(() => Request(server, request, response));
            }
        }

        private TwinoServer _server;

        public HttpServerTest()
        {
            IHttpRequestHandler requestHandler = new RequestHandler();
            _server = TwinoServer.CreateHttp(requestHandler);
            _server.Options.MaximumRequestLength = Int32.MaxValue;
        }

        [Fact]
        public void Run()
        {
            _server.Start(81);
            Assert.True(_server.IsRunning);
        }

        [Fact]
        public void Get()
        {
            _server.Start(82);

            HttpClient client = new HttpClient();
            var response = client.GetAsync("http://localhost:82/testing").Result;
            Assert.Equal(200, Convert.ToInt32(response.StatusCode));
        }

        [Fact]
        public void PostUTF8()
        {
            _server.Start(83);

            List<KeyValuePair<string, string>> form = new List<KeyValuePair<string, string>>();
            form.Add(new KeyValuePair<string, string>("firstName", "Mehmet"));
            form.Add(new KeyValuePair<string, string>("lastName", "Helvacikoylu"));
            form.Add(new KeyValuePair<string, string>("age", "32"));

            HttpContent content = new FormUrlEncodedContent(form);

            HttpClient client = new HttpClient();
            var response = client.PostAsync("http://localhost:83/testing", content).Result;
            Assert.Equal(200, Convert.ToInt32(response.StatusCode));
        }

        [Fact]
        public void PostASCII()
        {
            _server.Start(84);

            List<KeyValuePair<string, string>> form = new List<KeyValuePair<string, string>>();
            form.Add(new KeyValuePair<string, string>("firstName", "Mehmet"));
            form.Add(new KeyValuePair<string, string>("lastName", "Helvacikoylu"));
            form.Add(new KeyValuePair<string, string>("age", "32"));

            HttpContent content = new FormUrlEncodedContent(form);

            HttpClient client = new HttpClient();
            var response = client.PostAsync("http://localhost:84/testing", content).Result;
            Assert.Equal(200, Convert.ToInt32(response.StatusCode));
        }
    }
}