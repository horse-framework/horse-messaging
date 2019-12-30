
## Introducing to Twino HTTP

Twino is a TCP server.
If you want to listen incoming connections for HTTP protocol,
you can just call **UseHttp** method of twino server object.
Twino searches all registered protocols for each tcp connection.
If incoming data belongs to registered protocol, connection will continue with that protocol.

UseHttp method has some overloads. These overloads provides to implement interface or method.
You can also apply some settings on initialization.
We will see basic method implementation in sample core.
Here is how can HTTP protocol be registered.


    //create twino server for handling tcp connections
    TwinoServer server = new TwinoServer();

    //here is http protocol implementation
    //each request will trigger that method
    //and response will be written at here
    server.UseHttp(async (request, response) =>
    {
        response.StatusCode = HttpStatusCode.OK;
        await response.WriteAsync("Hello, World!");
    });

    //start server on port 80
    server.Start(80);
    
    //optional, you can hold the application at this line
    server.BlockWhileRunning();
    

Request and rsponse parameters have all properties you need while handling an http connection and writing it's response.
However, using HTTP protocol directly (with UseHttp method) is not a common way.
You can use UseHttp for basic http request handling.
If your application has many url in it, and you need to use ioc or you need authentication and authorization,
using Twino Mvc is the best way.

But technically, Twino MVC also uses this method to handle HTTP requests.
You can look **MvcConnectionHandler.cs** file in Twino.Mvc assembly to see how HTTP requests are handled by Twino MVC.
