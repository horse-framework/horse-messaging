## Introducing to Twino Server

Twino Server is a foundation for all protocol servers.
Twino Server can work alone, accepts the tcp connections.
If there is no protocol is implemented to the server, twino does not send any data to the clients.
Let's create an example which only accepts the connection requests.

    TwinoServer server = new TwinoServer();
    server.Start(30000);
    server.BlockWhileRunning();
  
In example above, our application lists port 80 and accept TCP connections.
You can check it from command line with

    nc -v localhost 30000

You receive

    Connection to localhost 30000 port [tcp/http] succeeded!
  
Message, that tells you the connection is accepted. Then you can stop application and try again. You receive

    nc: connect to localhost port 30000 (tcp) failed: Connection refused
  
Message, that tells you there is no application which is accepting TCP connections from that port.

Creating a TCP server is just that easy with twino!

In third line **BlockWhileRunning** method just blocks the code flow.
Because Twino Server's Start method starts the server asynchronously.
In many cases, your last code line will not be **Start** method,
you probably want to do something more after server started.

In order to apply some options to your server you can read [Twino Server Options](https://github.com/mhelvacikoylu/twino/blob/v2/docs/general/Options.MD) article.
