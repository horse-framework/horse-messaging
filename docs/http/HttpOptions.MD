## HTTP Options

HTTP options are set by HttpOptions class in Twino.Protocols.Http assembly.
When you create and HTTP or websocket server, even you are using directly Twino Mvc an HTTP options must be defined.
If you do not define any HTTP option, default options will be used.

HttpOptions class has a few properties listed below:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>HttpConnectionTimeMax</td>
      <td>
        Maximum keep alive time in seconds. If zero, tcp connection will be closed right after response is sent.
        An example, if it's 30, connection will keep alive up to 30 seconds and next http requests will be handled
        on same tcp connection.
        If your response does not have any content, this value will be ignored and connection will be closed.
        (because http protocol requires this).
        **Default is 300 seconds**
      </td>
    </tr>
    <tr>
      <td>MaximumRequestLength</td>
      <td>
        Maximum request length in bytes, including content. **Default is 100KB**
      </td>
    </tr>
    <tr>
      <td>SupportedEncodings</td>
      <td>
        Supported content encodings.
        Default, it's none.
        You can support gzip, brotli or deflate encodings.
        selection is made according to array indices.
        But if client does not support your high priority encoding, next will be tried.
        If client does not support any encodings you added, encoding will not be applied, data will sent as plain.
        **Default is none**
      </td>
    </tr>
    <tr>
      <td>Hostnames</td>
      <td>
        Supported hostnames.
        If null or empty array, all requests are accepted.
        If at least one hostname is defined,
        Only defined hostname requests are accepted.
        **Default is null**
      </td>
    </tr>
  </tbody>
</table>
