using System;

namespace Twino.Core
{
    /// <summary>
    /// WebSocket Protocol OP Codes
    /// </summary>
    public enum SocketOpCode : byte
    {
        /// <summary>
        /// When the data consists of multiple packages, until the last package, all packages must have continue definition
        /// </summary>
        Continue = 0x00,

        /// <summary>
        /// Sending text data definition
        /// </summary>
        UTF8 = 0x01,

        /// <summary>
        /// Sending binary data definition
        /// </summary>
        Binary = 0x02,

        /// <summary>
        /// Terminating connection definition
        /// </summary>
        Terminate = 0x08,

        /// <summary>
        /// Sending ping request definition
        /// </summary>
        Ping = 0x09,

        /// <summary>
        /// Sending response of the ping definition
        /// </summary>
        Pong = 0x0A
    }

    /// <summary>
    /// WebSocket protocol socket length types (1 byte, 3 bytes or 9 bytes)
    /// </summary>
    public enum SocketLength : byte
    {
        /// <summary>
        /// 1 byte length definition. Used when the message length shorter than 126
        /// </summary>
        Shorter126,

        /// <summary>
        /// 3 byte length definition. After first byte length type, next 2 bytes are unsigned short length
        /// </summary>
        Int16,

        /// <summary>
        /// 9 bytes length definition. After first byte length type, next 8 bytes are unsigned long length
        /// </summary>
        Int64
    }

    /// <summary>
    /// Reading status for PackageReader class
    /// </summary>
    public enum ReadingStatus : byte
    {
        /// <summary>
        /// In this status, package reader is reading the OP code
        /// </summary>
        OpCode,

        /// <summary>
        /// Finding if package has masking or not and type of the length definition
        /// </summary>
        MaskingAndLength,

        /// <summary>
        /// Reading length (2 bytes or 8 bytes)
        /// </summary>
        Length,

        /// <summary>
        /// Reading the key data
        /// </summary>
        Key,

        /// <summary>
        /// Reading message body
        /// </summary>
        Body
    }

    /// <summary>
    /// Reads Websocket message
    /// </summary>
    public class WebSocketReader
    {
        #region Properties

        /// <summary>
        /// WebSocket protocol Fin value
        /// </summary>
        public bool Fin { get; private set; }

        /// <summary>
        /// WebSocket protocol OpCode value
        /// </summary>
        public SocketOpCode OpCode { get; private set; }

        /// <summary>
        /// Message length type
        /// </summary>
        public SocketLength LengthType { get; private set; }

        /// <summary>
        /// Bytes of the length value (can be 1, 2 or 8 bytes)
        /// </summary>
        private byte[] _lengthBytes;

        /// <summary>
        /// Tells how many bytes of length is read
        /// Sometimes packages can be divided between length data
        /// In order to handle this kind of situations, we need to keep this data
        /// </summary>
        private int _lengthBytesRead;

        /// <summary>
        /// If package is masking (Masking is a messaging type of the websocket protocol)
        /// </summary>
        public bool Masking { get; private set; }

        /// <summary>
        /// Message length value (consists of the LengthBytes and created with bit converter)
        /// </summary>
        public int PayloadLength { get; private set; }

        /// <summary>
        /// Message body reader position
        /// </summary>
        private int _bodyReadIndex;

        /// <summary>
        /// Masking key
        /// </summary>
        private byte[] _key;

        /// <summary>
        /// Tells how many bytes of key is read
        /// Sometimes packages can be divided between key data
        /// In order to handle this kind of situations, we need to keep this data
        /// </summary>
        private int _keyBytesRead;

        /// <summary>
        /// Received message
        /// </summary>
        public byte[] Payload { get; set; }

        /// <summary>
        /// Reading status
        /// </summary>
        public ReadingStatus Status { get; private set; } = ReadingStatus.OpCode;

        /// <summary>
        /// If true, message is completely read and it's ready
        /// </summary>
        public bool IsReady { get; private set; }

        #endregion

        #region Read 

        /// <summary>
        /// Reads message from the buffer
        /// </summary>
        public int Read(byte[] buffer, int offset, int count)
        {
            //if the messaging continues in multiple network packages offset might be greater than 0
            //and we need to keep reading same message
            int read = offset;

            while (read < count)
            {
                //reads body
                if (Status == ReadingStatus.Body)
                {
                    byte[] readbuf = new byte[count - read];
                    Array.Copy(buffer, read, readbuf, 0, readbuf.Length);
                    read += ReadBody(readbuf);
                    return read - offset;
                }

                //reads header
                ReadHeader(buffer[read]);

                //keeps reading if not end of message
                read++;
                if (read >= count)
                    return read - offset;
            }

            //returns read / left / offset information. all data might not arrived yet.
            return read - offset;
        }

        /// <summary>
        /// Reads header data of the message
        /// </summary>
        public void ReadHeader(byte data)
        {
            if (Status == ReadingStatus.OpCode)
                ReadFinAndOpCode(data);

            else if (Status == ReadingStatus.MaskingAndLength)
                ReadMaskingAndLength(data);

            else if (Status == ReadingStatus.Length)
                ReadLength(data);

            else if (Status == ReadingStatus.Key)
                ReadMaskingKey(data);
        }

        /// <summary>
        /// Reads body of the message
        /// </summary>
        public int ReadBody(byte[] data)
        {
            //if we are just started to read body
            if (Payload == null)
            {
                Payload = new byte[PayloadLength];
                _bodyReadIndex = 0;
            }

            //calculates how many we read and how many left
            int left = Payload.Length - _bodyReadIndex;
            int read = data.Length > left ? left : data.Length;

            //reads into the Payload
            Array.Copy(data, 0, Payload, _bodyReadIndex, read);
            _bodyReadIndex += read;

            //checks if it's end of the message or not
            if (_bodyReadIndex >= Payload.Length)
            {
                //if there is masking, we need to check ACK
                if (Masking)
                {
                    for (int i = 0; i < Payload.Length; i++)
                        Payload[i] = (byte) (Payload[i] ^ _key[i % 4]);
                }

                IsReady = true;
            }

            return read;
        }

        #endregion

        #region Header

        /// <summary>
        /// Reads the first byte of the websocket message and figures out fin and op code values
        /// </summary>
        private void ReadFinAndOpCode(byte data)
        {
            byte opcode = data;
            if (opcode > 128)
            {
                Fin = true;
                opcode -= 128;
            }
            else
                Fin = false;

            if (opcode == 0x00 || opcode == 0x01 || opcode == 0x02 || opcode == 0x08 || opcode == 0x09 || opcode == 0x0A)
                OpCode = (SocketOpCode) opcode;

            Status = ReadingStatus.MaskingAndLength;
        }

        /// <summary>
        /// Reads masking and length bytes of the message
        /// </summary>
        private void ReadMaskingAndLength(byte data)
        {
            //figures out masking
            int plen = data;
            if (plen > 127)
            {
                Masking = true;
                _key = new byte[4];
                plen -= 128;
            }
            else
                Masking = false;

            //reads 1 byte length
            if (plen < 126)
            {
                PayloadLength = plen;
                LengthType = SocketLength.Shorter126;

                Status = !Masking ? ReadingStatus.Body : ReadingStatus.Key;

                if (Status != ReadingStatus.Key && OpCode != SocketOpCode.UTF8 && OpCode != SocketOpCode.Binary && OpCode != SocketOpCode.Continue)
                    IsReady = true;
            }

            //reads 3 (1 + short) bytes length
            else if (plen == 126)
            {
                LengthType = SocketLength.Int16;
                Status = ReadingStatus.Length;
                _lengthBytes = new byte[2];
                _lengthBytesRead = 0;
            }

            //reads 9 (1 + long) bytes length
            else if (plen == 127)
            {
                LengthType = SocketLength.Int64;
                Status = ReadingStatus.Length;
                _lengthBytes = new byte[8];
                _lengthBytesRead = 0;
            }
        }

        /// <summary>
        /// When the length of the message is greater than 126
        /// length value must be read byte by byte
        /// This method reads the bytes of the length value and calculates payload length
        /// </summary>
        private void ReadLength(byte data)
        {
            _lengthBytes[_lengthBytes.Length - _lengthBytesRead - 1] = data;
            _lengthBytesRead++;

            if (_lengthBytesRead >= _lengthBytes.Length)
            {
                if (LengthType == SocketLength.Int16)
                    PayloadLength = BitConverter.ToUInt16(_lengthBytes, 0);
                else if (LengthType == SocketLength.Int64)
                    PayloadLength = (int) BitConverter.ToUInt64(_lengthBytes, 0);

                Status = Masking ? ReadingStatus.Key : ReadingStatus.Body;
            }
        }

        /// <summary>
        /// Reads the masking key of the message
        /// </summary>
        private void ReadMaskingKey(byte data)
        {
            _key[_keyBytesRead] = data;
            _keyBytesRead++;

            if (_keyBytesRead == 4)
            {
                Status = ReadingStatus.Body;
                if (OpCode != SocketOpCode.UTF8 && OpCode != SocketOpCode.Binary && OpCode != SocketOpCode.Continue)
                    IsReady = true;
            }
        }

        #endregion
    }
}