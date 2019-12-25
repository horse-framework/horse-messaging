## Messaging Queue Options

In Twino MQ, we use options in three sections:
* Queue
* Channel
* Server general

These three sections are subdivisions of each other.
Because of this twino uses inheritance for these section options.
This allows us to define default options for sub section.

### Channel Queue Options

Channel queue options uses **ChannelQueueOptions** class. Here is the property list:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>SendOnlyFirstAcquirer</td>
      <td>
        If true, messages will be sent to only first acquirer. Acquirer order is by connection time.
        If you set this value, messages may sent to same acquirer everytime.
        If you want to round-robin, you can do it in interface implementation.
        Default value is **false**.
      </td>
    </tr>
    <tr>
      <td>RequestAcknowledge</td>
      <td>
        If true, the messages in the queue will sent with request acknowledge option.
        Each consumer will send acknowledge message. These messages will trigger an action in your interface implementation.
        If you want to know if messages are received to consumers, setting this value true is a good choice.
        But if you have too many consumers in queue, this value may cause more network usage.
        Default value is **false**.
      </td>
    </tr>
    <tr>
      <td>AcknowledgeTimeout</td>
      <td>
        When RequestAcknowledge is true, this value is for maximum acknowledge waiting duration.
        If in this duration, client doesn't send acknowledge, acknowledge timed out events will be triggered.
        Default value is **15 seconds**.
      </td>
    </tr>
    <tr>
      <td>MessageTimeout</td>
      <td>
        When a message is pushed to the queue, if there is no receivers or queue status is pull.
        Message has a timeout duration. After this duration, message will removed from the queue.
        In order to disable message timeout, set it TimeSpan.Zero. Default value is **TimeSpan.Zero**.
      </td>
    </tr>
    <tr>
      <td>UseMessageId</td>
      <td>
        If true, each message has a unique id. This is good for recognizing messages, sending responses and acknowledges.
        If you have a network usage concerns and you do not need to use response or acknowledge you can disable this.
        Default value is **true**.
      </td>
    </tr>
    <tr>
      <td>WaitForAcknowledge</td>
      <td>
        If true, after a message is sent, the next message will not process.
        Queue will wait for the previous message's acknowledge.
        If you don't want to send next message before previous message process has done on client side,
        you can enable this. Default value is **false**.
      </td>
    </tr>
    <tr>
      <td>HideClientNames</td>
      <td>
        If true, consumers can see producer's unique id in Source property of the Tmq Message.
        This can be useful, when a client wants to send a directly message to producer or when consumers needs to know
        to pushed this message. You can disable this for more privacy. Default value is **false**.
      </td>
    </tr>
    <tr>
      <td>Status</td>
      <td>
        Queue default status. This property is used only when the queue is created, for defining first status value.
        Status can be Route, Push, Pull, Paused or Stopped.
        For more information you can read <a href="https://github.com/mhelvacikoylu/twino/blob/v2/docs/mq/Queue%20Statuses.MD">Queue Statuses</a> article.
      </td>
    </tr>
  </tbody>
</table>


## Channel Options

Channel options uses **ChannelOptions** class. This class derives from ChannelQueueOptions.
That means, Channel options has all options channel queue options has.
This is useful, when you create a new queue without specifiying options, queue will use channel default options. So, if you set a default options to channel, all queue options will use these options.

Here is the list of properties channel options has and channel queue options does not have:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>AllowMultipleQueues</td>
      <td>
        If true, multiple queues can be created in the channel. Default value is **true**.
      </td>
    </tr>
    <tr>
      <td>AllowedQueues</td>
      <td>
        If it has at least one value, channel will allow to create queues only allowed in this list.
        This list a number array. If you do not restrict content types, you can set it null or empty array.
        Default value is **null**.
      </td>
    </tr>
  </tbody>
</table>

## Server Options

Server options uses **MqServerOptions** class. This class derives from ChannelOptions.
That means, server options has all options channel options has so has all options channel queue options has too.
So you can set a default channel and queue options with server options object.
For creating new channels or queuest with no specified options default server options are used.

Here is the list of properties server options has and channel options does not have:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Name</td>
      <td>
        Name of the server. This name is used when server is connected to other distributed instances.
      </td>
    </tr>
    <tr>
      <td>Type</td>
      <td>
        Type of the server. This type is used when server is connected to other distributed instances.
      </td>
    </tr>
    <tr>
      <td>Instances</td>
      <td>
        Other server instances. For distributed systems, you may want to create multiple queue servers that work together.
      </td>
    </tr>
  </tbody>
</table>

### Instance options

Instance options in server options properties:

<table>
  <thead>
    <tr>
      <th>Name</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Name</td>
      <td>
        Name of the connecting remote server. This is useful for describing and monitoring.
      </td>
    </tr>
    <tr>
      <td>Host</td>
      <td>
        Hostname of the server. Should be like tmq://host-name:port
      </td>
    </tr>
    <tr>
      <td>Token</td>
      <td>
        Authentication token. Security may be necessary, you can create token for each instance and while connecting to other instances they can verify token.
      </td>
    </tr>
    <tr>
      <td>KeepMessages</td>
      <td>
        If true messages will be kept while there are active instance connections.
        When a instance connection is established, all queued messages are sent.
        As more technical description is, if this value is true, connections will be handled with absolute connectors.
        If false, connections will be handled with sticky connectors.
        Default value is **false**.
      </td>
    </tr>
    <tr>
      <td>ReconnectWait</td>
      <td>
        If server instance connection fails or disconnects, the time wait before reconnect.
      </td>
    </tr>
  </tbody>
</table>
