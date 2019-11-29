# TMQ Protocol

- Required header frame minimum size is 8 bytes.
- Header frame can be extended if message has a unique id, source id or target id.

## Protocol Frame

     0 1 234567 8 9 012345
    +-+-+------+-+-+------+
    |F|P| TYPE |R|D| TTL  | -> 2 Octets
    |A|R|  (6) |S|E| (6)  |
    -----------------------
    |  Message ID Length  | -> 1 Octet
    -----------------------
    |   Source Id Length  | -> 1 Octet
    -----------------------
    |   Target Id Length  | -> 1 Octet
    -----------------------
    |     Content Type    | -> 2 Octets (unsigned short)
    -----------------------
    |    Payload Length   | -> Flexible length, min 1 octet, max 9 octets
    -----------------------
    |        Payload      | -> n bytes (n: payload length)
    -----------------------
    

#### FA: First Acquirer
First acquirer bit. If ONE, message's first acquirer is the receiver. If ZERO, message sent to another before.

#### PR: Priority
Priority bit. If ONE, message is high priority message SHOULD be added to the beginning of the queue. If ZERO, message is standard, SHOULD be added to end of the queue.

#### Type
Message type in 6 bits. In TMQ 1.0, supported types are below,

     (0x00) Other     : All other message types.
     (0x08) Terminate : Client requests to terminate the connection.
     (0x09) Ping      : PING message from server to client.
     (0x0A) Pong      : PONG message from client to server.
     (0x10) Server    : Client sends a message to directly server. Message SHOULD process in server.
     (0x11) Channel   : Client sends a message to a queue. Message SHOULD be transmitted to observers.
     (0x12) Client    : Client sends a message to another client. Server SHOULD transmit the message.
     (0x13) Delivery  : Client sends a delivery message for another message that received before.
     (0x14) Response  : Client sends a response message for another message that received before.
     (0x15) Redirect  : Client or server redirects the message another receiver.

#### RS: Response Required
If message needs a response this MUST be ONE, If response isn't waited, this value MUST be ZERO.

#### DE: Delivery Required
If message needs a delivery message this MUST be ONE, If delivery is not required, this value MUST be ZERO.

#### TT: Time To Live
TMQ Protocol TTL value in 6 bits (0-63)

#### Message Id Length
Message id length is unsigned byte. If message doesn't have a unique id, this value can be ZERO. If message has a unique id, this message can be between (0-255]. Message Unique Id value can't be larger than 255 bytes.

#### Source Id Length
Length of message sender's id value in bytes. Works like message id length. If message is sending from it's real source (from client) to the server, source CAN be empty, so source id will be ZERO. Server will set message source as sender's unique id. If message is sent from server to a receiver, source SHOULD NOT be empty if message's real source is not server's itself. This information will be explained in Source Id section.

#### Target Id Length
Length of message target's id value. When message is sending directly to the server, this value can be zero. Because Server message type does not require a target, type of the message tells us the target. But if message is sent to a queue, target MUST be the channel name of the queue. If message is sent to another client, target MUST be the unique id of the client.

#### Content Type
Content type is unsigned short value. Gives us a description of the content, what type it is. Each channel has it's own content types. You can define different type of message with same content types in different channels. Peer to peer messages have their own content types, and server messages have their own content types such as hello, join channel, leave channel etc.

#### Payload Length
Flexible size content length. If value is less than 253 bytes, the value is the content length. If value is 253, next two octets contains payload length as unsigned short. If value is 254, next four octets contains payload length as unsigned int32. If value is 255, next eight octets contains payload length as unsigned int64.

          Value < 253 => Payload length is value
          Value = 253 => Payload is in next 2 bytes contains unsigned short.
          Value = 254 => Payload is in next 4 bytes contains unsigned int32.
          Value = 255 => Payload is in next 8 bytes contains unsigned int64.

#### Payload
Message content as byte array. That value can be everything you want.

## TMQ Protocol Handshaking
...

#### Protocol Message From Client
...

#### Protocol Message From Server
...

#### Hello From Client
...

#### Hello From Server
...

## Message Types
...


### Server Message
...


### Channel Message
Channel messages are send from procuder clients. When producer sends the message to the server, server puts the message into the queue and starts to send it's receivers. A channel message has two transmission, first from producer to server, second from server to consumer. So, when you see a message is being sent from somewhere to somewhere, you can recognize the workflow by learning if sender was client or server.

**First Acquirer:** While client sending a message to the channel, server does not care the value, it sets this value as True. Then, starts to send this message to it's receivers. True to first receiver, False to others. If message is tried to send before and somehow message sending operation is skipped, on next try False to all receivers.

**High Priority:** Producer client sends the message with a priority flag. If it's high priority, server puts the message at the beginning of the queue. While server sending message to it's consumers, server does not change this value. So, consumers can see if the message has high priority.

**Type:** In channel messages, message type is always Channel (0x11) even from producer to server or from server to consumer.

**ResponseRequired:** Usually, response required flag is used for peer to peer messaging. But this can be used in queues too. If producer sends message with response required flag, server will transmit the message to consumers with this flag. Consumers will try to response. You should do this in client-side manually. When response is sent from consumer, server will trigger response received method. In the method, you need to verify if response should send to producer or not.

**DeliveryRequired:** After server send the message to consumer, if delivery requested, consumer will send a delivery message to the server and server will trigger delivery received methods. Delivery required option is set in server side. Server does not care what was the value from producer.

**MessageId:** Message unique id, can be null from producer, if in server options, UseMessageId is false, server will not set a unique message id. If you want to lower your bandwidth, increase your performance and responses and deliveries are not important, just don't use message id.

**SourceId:** Source Id is the producer id. If HideClientNames option is enabled in server, value will be null.

**TargetId:** Target Id is the channel id.


### Client Message
Client messages are peer to peer messages. They are sent to server, server finds the receiver and transmit the message. In client messages, server does not care anything. Everything is in between clients.

First Acquirer, High Priority, Response Required and Delivery Required are same with what client sends, server does not touch these values, so sender and receiver can tell each other some messages with these values.

**MessageId:** Message id can be null if not required

**SourceId:** Should be sender unique id.

**TargetId:** Should be receiver unique id. This is the most important value in client messages. If you type this value wrong, message will sent to another receiver (if unique id matches, depends on your id generator system)


### Delivery Message
Delivery messages have some values same with delivered message such as MessageId, ContentType. They should not be confused with real messages. If the type of the message delvery, you should know these values are delivered message's values.

FirstAcquirer and High Priority are same with delivered message. ResponseRequired and DeliveryRequired values are not important and they are usually false.

**Type:** Delivery is always Delivery (0x13)

**MessageId:** Delivery message doesn't have it's own MessageId, MessageId value is the id of the delivered message.

**TargetId:** Delivered message's channel name

**ContentType:** Delivered message's content type.


### Response Message
Response message does not have it's own MessageId. The message id value of the message is same with the message that requested the response.

FirstAcquirer and High Priority are same with delivered message. DeliveryRequired value are not important and they are usually false.

**ResponseRequired:** True, if response requires another response. Next response MessageId will be same again.

**Type:** Type is always Response (0x14)

**MessageId:** Same with requested message id.

**SourceId:** Id of the client that sending response message.

**TargetId:** Id of the client that sent request message.

**ContentType:** Response message's own content type. Requested client will use this value to recognize message type.


### PING and PONG
PING and PONG messages are constant messages. PING is sent from server and PONG is sent from client. TMQ procotol required minimum 8 octets frame header data. Because of this, PING and PONG messages should be 8 octets. PING and PONG definition in first byte, second byte is 0xFF and all other 6 bytes should be filled as 0x00.

     PING: 0x89 0xFF 0x00 0x00 0x00 0x00 0x00 0x00
     PONG: 0x8A 0xFF 0x00 0x00 0x00 0x00 0x00 0x00
