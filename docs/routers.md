# Horse Messaging Documentation

[Back to Documentation Contents](README.md)

## Routers

Routers transmit messages to their bindings. Each router can have one or more binding.
Routers supports some binding algorithms by default. And you can also create your router and bindings.

When a message received from client with Router message type. The target name should be a router name created before.
Creating a router can be done with message headers, from server manually, from client manually or from jockey administration website.
Router decides how the message will be handled depending on router's method. Router chooses one or more bindings and sends the message to these bindings.
Each binding has it's own method too and binding redirects the message to the real target such as queue, direct, channel, http endpoint or somewhere else.

### Route Methods

There are 2 router methods:

1. **Distribute:** The message is sent to all bindings. Real message is cloned for each binding with same message id.
2. **Round Robin:** Each message is redirect to only one binding with round robin algorithm.
3. **Only First:** Only first available binding receives the message. When the first binding is unavailable, next binding will be first binding.

### Bindings and Binding Types

Bindings are added into routers. Each router can have multiple bindings and sends message to bindings depending on router's method.
And bindings have their own methods too. These methods are same and they sends the message to binding targets depending on their methods.
And bindings also have Interaction property. Interaction value determines the behavior of the response of the message.
Interaction property can take two values: None or Response.

As an example, a producer sent a message to a router. Router received the message and redirected it to it's binding.
Binding is a queue binding and it's Interaction property is Response. When message is pushed into the queue, the routers
sends a positive response message to the producer, because it has a interaction value.

You can create your own binding by inheriting Binding class. Horse has some useful bindings by default.
Here are predefined bindings in Horse:

1. **Direct Binding:** Sends the message to one or multiple clients directly. The direct target can be by type, name or client id. And it can have a filter with * and ? joker characters.
2. Queue Binding: x
3. Dynamic Queue Binding: x
4. Auto Queue Binding: x
5. Topic Binding: x
6. Http Binding: x

### Creating Routers and Bindings

### Publishing Messages to Routers

