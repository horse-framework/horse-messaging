using Twino.Server;
using System;
using System.Threading.Tasks;
using Twino.Client;
using Xunit;

namespace Test.Server
{
    public class PackageSizeTest
    {

        [Theory]
        [InlineData(2000, 1)]
        [InlineData(2000, 2)]
        [InlineData(2000, 3)]
        public void FromClientToServerSendAndReceivePackages(int packageSize, int howManyTimesPerPackageWillSent)
        {
            bool finished = false;

            TwinoServer twino = TwinoServer.CreateWebSocket(async (s, r, c) =>
            {
                ServerSocket socket = new ServerSocket(s, r, c);
                socket.Disconnected += sc =>
                {
                    Assert.True(finished);
                };
                socket.MessageReceived += (sc, rm) =>
                {
                    Assert.True(rm.Length > 0);
                };
                return await Task.FromResult(socket);
            });

            int port = 100 + howManyTimesPerPackageWillSent;
            twino.Start(port);
            System.Threading.Thread.Sleep(500);

            TwinoClient client = new TwinoClient();
            client.Disconnected += cs => Assert.True(finished);
            client.Connect("127.0.0.1", port, false);
            System.Threading.Thread.Sleep(500);
            Assert.True(client.IsConnected);

            string data = Guid.NewGuid().ToString();
            while (data.Length < packageSize)
                data += Guid.NewGuid().ToString();

            for (int i = 1; i <= packageSize; i++)
            {
                string msg = data.Substring(0, i);
                for (int k = 0; k < howManyTimesPerPackageWillSent; k++)
                {
                    bool sent = client.Send(msg);
                    System.Threading.Thread.Sleep(2);
                    Assert.True(sent);
                    Assert.True(client.IsConnected);
                }
            }
            System.Threading.Thread.Sleep(250);
            Assert.True(client.IsConnected);
            finished = true;
            System.Threading.Thread.Sleep(250);
            client.Disconnect();
        }

        [Theory]
        [InlineData(2000, 1)]
        [InlineData(2000, 2)]
        [InlineData(2000, 3)]
        public void FromServerToClientSendAndReceivePackages(int packageSize, int howManyTimesPerPackageWillSent)
        {
            bool finished = false;

            TwinoServer twino = TwinoServer.CreateWebSocket(async (s, r, c) =>
            {
                ServerSocket socket = new ServerSocket(s, r, c);

                socket.Connected += sc =>
                {
                    string data = Guid.NewGuid().ToString();
                    while (data.Length < packageSize)
                        data += Guid.NewGuid().ToString();

                    for (int i = 1; i <= packageSize; i++)
                    {
                        string msg = data.Substring(0, i);
                        for (int k = 0; k < howManyTimesPerPackageWillSent; k++)
                        {
                            bool sent = sc.Send(msg);
                            System.Threading.Thread.Sleep(2);
                            Assert.True(sent);
                            Assert.True(sc.IsConnected);
                        }
                    }
                    System.Threading.Thread.Sleep(250);
                    Assert.True(sc.IsConnected);
                    finished = true;
                    System.Threading.Thread.Sleep(250);
                    sc.Disconnect();
                };

                socket.Disconnected += sc =>
                {
                    Assert.True(finished);
                };
                socket.MessageReceived += (sc, rm) =>
                {
                    Assert.True(rm.Length > 0);
                };
                return await Task.FromResult(socket);
            });

            int port = 110 + howManyTimesPerPackageWillSent;
            twino.Start(port);
            System.Threading.Thread.Sleep(500);

            TwinoClient client = new TwinoClient();
            client.Disconnected += cs => Assert.True(finished);
            client.Connect("127.0.0.1", port, false);
            System.Threading.Thread.Sleep(500);
            Assert.True(client.IsConnected);

            while (!finished)
                System.Threading.Thread.Sleep(1000);

        }

        [Theory]
        [InlineData(8000, 1, 2)]
        [InlineData(8000, 2, 2)]
        [InlineData(8000, 3, 2)]
        public void MutualLongTermConnection(int packageSize, int howManyTimesPerPackageWillSent, int millisecondsBetweenPerPackages)
        {
            string data = Guid.NewGuid().ToString();
            while (data.Length < packageSize + 23)
                data += Guid.NewGuid().ToString();

            bool finished = false;

            TwinoServer twino = TwinoServer.CreateWebSocket(async (s, r, c) =>
            {
                ServerSocket socket = new ServerSocket(s, r, c);

                socket.Connected += sc =>
                {
                    for (int i = 1; i <= packageSize; i += 13)
                    {
                        if (finished)
                            break;

                        string msg = data.Substring(0, i);

                        for (int k = 0; k < howManyTimesPerPackageWillSent; k++)
                        {
                            if (finished)
                                break;

                            bool sent = sc.Send(msg);
                            Assert.True(sent);
                        }
                        System.Threading.Thread.Sleep(millisecondsBetweenPerPackages);
                        Assert.True(sc.IsConnected);
                    }
                };

                socket.Disconnected += sc =>
                {
                    Assert.True(finished);
                };
                socket.MessageReceived += (sc, rm) =>
                {
                    Assert.True(rm.Length > 0);
                };
                return await Task.FromResult(socket);
            });

            int port = 120 + howManyTimesPerPackageWillSent;
            twino.Start(port);
            System.Threading.Thread.Sleep(2500);

            TwinoClient client = new TwinoClient();
            client.Disconnected += cs => Assert.True(finished);

            client.Connect("127.0.0.1", port, false);
            System.Threading.Thread.Sleep(2500);
            Assert.True(client.IsConnected);

            for (int i = 1; i <= packageSize; i++)
            {
                string msg = data.Substring(0, i);
                for (int k = 0; k < howManyTimesPerPackageWillSent; k++)
                {
                    bool sent = client.Send(msg);
                    System.Threading.Thread.Sleep(millisecondsBetweenPerPackages);
                    Assert.True(sent);
                    Assert.True(client.IsConnected);
                }
            }
            System.Threading.Thread.Sleep(2500);
            Assert.True(client.IsConnected);
            finished = true;
            System.Threading.Thread.Sleep(2500);
            client.Disconnect();

        }

    }
}
