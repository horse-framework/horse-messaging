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
