## Twino Server Options

We have discussed to creating Twino Server in [Introducing to Twino Server](https://github.com/mhelvacikoylu/twino/blob/v2/docs/general/Introduce.MD) article.
In this article, we will talk about the server options.

    ServerOptions options = new ServerOptions();
    options.RequestTimeout = 60;

    TwinoServer server = new TwinoServer(options);
    server.Start(30000);
    server.BlockWhileRunning();
    
The example code above, creates new twino server with 60 seconds timeout.
That means, when a client is connected to the server,
If no protocol resolves in 60 seconds, server disconnects the client's connection.


There are a few options in core Twino Server. Here is the full list:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>PingInterval</td>
      <td>For long term connections ping interval in seconds (eg: 300 secs for websockets, 240 secs for tmq etc)</td>
    </tr>
    <tr>
      <td>MaximumPendingConnections</td>
      <td>Maximum pending connections in OS. By default it's unlimited, When too many connection request is received, you can ignore some of them with setting a value such as 250, 500, 1024.</td>
    </tr>
    <tr>
      <td>Hosts</td>
      <td>HostOptions array. Each HostOption has it's own properties that described below</td>
    </tr>
    <tr>
      <td>RequestTimeout</td>
      <td>Request timeout is the maximum time to handle the connection with a protocol. For HTTP it's the response time, for WebSocket or TMQ it's the handshake time.</td>
    </tr>
    <tr>
      <td>ContentEncoding</td>
      <td>Supported content encodings. For plain data, let this value null. "gzip" and "brotli" are supported. You can set value like "gzip,brotli" or just "br"</td>
    </tr>
  </tbody>
</table>


And here is the Host Options:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Port</td>
      <td>Listening port number</td>
    </tr>
    <tr>
      <td>Hostnames</td>
      <td>Accepted hostnames for this port. It's string array. null or empty array accepts all hostnames.</td>
    </tr>
    <tr>
      <td>SslEnabled</td>
      <td>If true, connections will be SSL protected.</td>
    </tr>
    <tr>
      <td>SslCertificate</td>
      <td>SSL Certificate filename</td>
    </tr>
    <tr>
      <td>CertificateKey</td>
      <td>If there is, SSL Certificate password</td>
    </tr>
    <tr>
      <td>BypassSslValidation</td>
      <td>If true, SSL errors will be ignored</td>
    </tr>
    <tr>
      <td>SslProtocol</td>
      <td>SSL Protocol. Default is Tls12. Accepted values: None, Ssl2, Ssl3, Tls, Tls11, Tls12. None means use default.</td>
    </tr>
  </tbody>
</table>

Options can be created programmatically. Can be deserialized from a JSON. Maybe you want to keep JSON string in a database like MongoDB. Or you can load settings from file. Twino supports loading options from file. For example:

        TwinoServer server = new TwinoServer("options.json");
