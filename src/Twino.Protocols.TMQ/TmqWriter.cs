using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Message writer
    /// </summary>
    public class TmqWriter
    {
        /// <summary>
        /// Writes a TMQ message to stream
        /// </summary>
        public static void Write(TwinoMessage value, Stream stream, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;

            using MemoryStream ms = new MemoryStream();
            WriteFrame(ms, value, hasAdditionalHeader);

            if (value.HasHeader || hasAdditionalHeader)
                WriteHeader(ms, value, additionalHeaders);

            if (value.Length > 0)
                WriteContent(ms, value);

            ms.WriteTo(stream);
        }

        /// <summary>
        /// Creates byte array of TMQ message
        /// </summary>
        public static byte[] Create(TwinoMessage value, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;
            using MemoryStream ms = new MemoryStream();
            WriteFrame(ms, value, hasAdditionalHeader);

            if (value.HasHeader || hasAdditionalHeader)
                WriteHeader(ms, value, additionalHeaders);

            if (value.Length > 0)
                WriteContent(ms, value);

            return ms.ToArray();
        }

        /// <summary>
        /// Creates byte array of only TMQ message frame
        /// </summary>
        public static byte[] CreateFrame(TwinoMessage value)
        {
            using MemoryStream ms = new MemoryStream();
            WriteFrame(ms, value, false);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates byte array of only TMQ message content
        /// </summary>
        public static byte[] CreateContent(TwinoMessage value)
        {
            using MemoryStream ms = new MemoryStream();
            WriteContent(ms, value);
            return ms.ToArray();
        }

        /// <summary>
        /// Writes frame to stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteFrame(MemoryStream ms, TwinoMessage message, bool hasAdditionalHeaders)
        {
            byte proto = (byte) message.Type;

            if (message.WaitResponse)
                proto += 128;

            if (message.HighPriority)
                proto += 64;

            if (message.HasHeader || hasAdditionalHeaders)
                proto += 32;

            ms.WriteByte(proto);
            byte reserved = 0;
            ms.WriteByte(reserved);
            ms.WriteByte((byte) message.MessageIdLength);
            ms.WriteByte((byte) message.SourceLength);
            ms.WriteByte((byte) message.TargetLength);

            ms.Write(BitConverter.GetBytes(message.ContentType));

            if (message.Content != null && message.Length == 0)
                message.Length = (ulong) message.Content.Length;

            if (message.Length < 253)
                ms.WriteByte((byte) message.Length);
            else if (message.Length <= ushort.MaxValue)
            {
                ms.WriteByte(253);
                ms.Write(BitConverter.GetBytes((ushort) message.Length));
            }
            else if (message.Length <= uint.MaxValue)
            {
                ms.WriteByte(254);
                ms.Write(BitConverter.GetBytes((uint) message.Length));
            }
            else
            {
                ms.WriteByte(255);
                ms.Write(BitConverter.GetBytes(message.Length));
            }

            if (message.MessageIdLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.MessageId);
                ms.Write(bytes);
            }

            if (message.SourceLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Source);
                ms.Write(bytes);
            }

            if (message.TargetLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Target);
                ms.Write(bytes);
            }
        }

        /// <summary>
        /// Writes header length and content to stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteHeader(MemoryStream ms, TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders)
        {
            using MemoryStream headerStream = new MemoryStream();

            if (message.HeadersList != null)
                foreach (KeyValuePair<string, string> pair in message.HeadersList)
                    headerStream.Write(Encoding.UTF8.GetBytes(pair.Key + ":" + pair.Value + "\r\n"));

            if (additionalHeaders != null)
                foreach (KeyValuePair<string, string> pair in additionalHeaders)
                    headerStream.Write(Encoding.UTF8.GetBytes(pair.Key + ":" + pair.Value + "\r\n"));

            ms.Write(BitConverter.GetBytes((ushort) headerStream.Length));
            headerStream.Position = 0;
            headerStream.WriteTo(ms);
        }

        /// <summary>
        /// Writes content to stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteContent(MemoryStream ms, TwinoMessage message)
        {
            if (message.Length > 0 && message.Content != null)
                message.Content.WriteTo(ms);

            ms.Position = 0;
        }
    }
}