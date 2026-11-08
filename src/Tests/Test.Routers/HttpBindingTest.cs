using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

public class HttpBindingTest
{
    /// <summary>
    /// Local HTTP endpoint that answers every request with a fixed status code, or never answers
    /// </summary>
    private sealed class TestEndpoint : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly HttpStatusCode _status;
        private readonly bool _respond;
        private int _requests;

        public string Url { get; }
        public string LastQuery { get; private set; }
        public int Requests => Volatile.Read(ref _requests);

        public TestEndpoint(HttpStatusCode status, bool respond = true)
        {
            _status = status;
            _respond = respond;

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            Url = $"http://localhost:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _ = Task.Run(Listen);
        }

        private async Task Listen()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                LastQuery = context.Request.Url?.Query;
                Interlocked.Increment(ref _requests);
                if (!_respond)
                    continue;

                context.Response.StatusCode = (int)_status;
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
                // already closed
            }
        }
    }

    private static Router CreateRouter(TestHorseRider server, string url, HttpBindingMethod method = HttpBindingMethod.Post)
    {
        Router router = new Router(server.Rider, "http-router", RouteMethod.Distribute);
        router.AddBinding(new HttpBinding
        {
            Name = "http",
            Target = url,
            ContentType = (ushort)method,
            Interaction = BindingInteraction.None
        });

        server.Rider.Router.Add(router);
        return router;
    }

    private static HorseMessage CreateMessage()
    {
        HorseMessage message = new HorseMessage(MessageType.Router, "http-router", 0);
        message.SetMessageId(Guid.NewGuid().ToString());
        message.SetStringContent("{\"value\":1}");
        return message;
    }

    [Fact]
    public async Task Post_SuccessfulEndpoint_EveryMessageIsRouted()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            using TestEndpoint endpoint = new TestEndpoint(HttpStatusCode.OK);
            Router router = CreateRouter(server, endpoint.Url);

            for (int i = 0; i < 5; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            Assert.Equal(5, endpoint.Requests);
        });
    }

    [Fact]
    public async Task Post_FailingEndpoint_IsNotRouted()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            using TestEndpoint endpoint = new TestEndpoint(HttpStatusCode.InternalServerError);
            Router router = CreateRouter(server, endpoint.Url);

            Assert.Equal(RouterPublishResult.NoReceivers, await router.Publish(null, CreateMessage()));
            Assert.Equal(1, endpoint.Requests);
        });
    }

    [Fact]
    public async Task RequestTimeout_LimitsWaitForEndpointThatDoesNotAnswer()
    {
        TimeSpan original = HttpBinding.RequestTimeout;
        try
        {
            HttpBinding.RequestTimeout = TimeSpan.FromMilliseconds(300);

            await TestHorseRider.RunWith(async (server, port) =>
            {
                using TestEndpoint endpoint = new TestEndpoint(HttpStatusCode.OK, respond: false);
                Router router = CreateRouter(server, endpoint.Url);

                Stopwatch watch = Stopwatch.StartNew();
                RouterPublishResult result = await router.Publish(null, CreateMessage());
                watch.Stop();

                Assert.Equal(RouterPublishResult.NoReceivers, result);
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Publish waited {watch.Elapsed}");
            });
        }
        finally
        {
            HttpBinding.RequestTimeout = original;
        }
    }

    /// <summary>
    /// The content is the query string. A target with a query string gets it after "&amp;", not a second "?".
    /// </summary>
    [Theory]
    [InlineData("", "?a=2")]
    [InlineData("?key=1", "?key=1&a=2")]
    public async Task Get_ContentIsAppendedToTargetQuery(string targetQuery, string expectedQuery)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            using TestEndpoint endpoint = new TestEndpoint(HttpStatusCode.OK);
            Router router = CreateRouter(server, endpoint.Url + targetQuery, HttpBindingMethod.Get);

            HorseMessage message = new HorseMessage(MessageType.Router, "http-router", 0);
            message.SetMessageId(Guid.NewGuid().ToString());
            message.SetStringContent("a=2");

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, message));
            Assert.Equal(expectedQuery, endpoint.LastQuery);
        });
    }
}
