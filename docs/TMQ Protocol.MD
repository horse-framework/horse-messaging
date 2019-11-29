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
    |    Payload Length   | -> 7-n bit encoded, min 1 byte, max 9 bytes
    -----------------------
    |        Payload      | -> n bytes (n: payload length)
    -----------------------
    

#### FA: First Acquirer
First acquirer bit. If ONE, message's first acquirer is the receiver. If ZERO, message sent to another before.

#### PR: Priority
Priority bit. If ONE, message is high priority message SHOULD be added to the beginning of the queue. If ZERO, message is standard, SHOULD be added to end of the queue.

#### Type
Message type in 6 bits. In TMQ 1.0, supported types are below,

  (0x00) Other     : All other message types<br>
  (0x08) Terminate : Client requests to terminate the connection.<br>
  (0x09) Ping      : PING message from server to client<br>
  (0x0A) Pong      : PONG message from client to server<br>
  (0x10) Server    : Client sends a message to directly server. Message SHOULD process in server.<br>
  (0x11) Channel   : Client sends a message to a queue. Message SHOULD be transmitted to queue observers.<br>
  (0x12) Client    : Client sends a message to another client. Server SHOULD find the receiver and transmit the message.<br>
  (0x13) Delivery  : Client sends a delivery message for another message that received before.<br>
  (0x14) Response  : Client sends a response message for another message that received before.<br>
  (0x15) Redirect  : Client or server redirects the message another receiver.<br>

#### RS: Response Required
If message needs a response this MUST be ONE, If response isn't waited, this value MUST be ZERO.

#### DE: Delivery Required
If message needs a delivery message this MUST be ONE, If delivery is not required, this value MUST be ZERO.

#### TT: Time To Live
TMQ Protocol TTL value in 6 bits (0-63)

#### Message Id Length
Message id length is unsigned byte. If message doesn't have a unique id, this value can be ZERO. If message has a unique id, this message can be between (0-255]. Message Unique Id value can't be larger than 255 bytes.

## Source Id Length
Length of message sender's Id value in bytes. Works like message id length. If message is sending from it's real source (from client) to the server, source CAN be empty, so source id will be ZERO. Server will set message source as sender's unique id. If message is sent from server to a receiver, source SHOULD NOT be empty if message's real source is not server's itself. This information will be explained in Source Id section.

#### Target Id Length
..

#### Content Type
..

#### Payload Length
..

#### Payload
..

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

#### Server Message
...

#### Channel Message
...

#### Client Message
...

#### Delivery Message
...

#### Response Message
...

#### PING and PONG
...
