using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Persistency;

public class CorruptDatabaseLoadTest
{
    [Fact]
    public async Task Open_CorruptInsertRecord_DoesNotConsumeFollowingValidRecord()
    {
        string dataPath = $"db-corrupt-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        Directory.CreateDirectory(dataPath);
        string filename = Path.Combine(dataPath, "queue.hdb");

        try
        {
            await using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                DataMessageSerializer serializer = new DataMessageSerializer();
                await serializer.Write(stream, CreateMessage("ok-001", "first"));
                await WriteCorruptInsert(stream, "bad-001", 64);
                await serializer.Write(stream, CreateMessage("ok-002", "second"));
                await stream.FlushAsync();
            }

            Database database = new Database(new DatabaseOptions
            {
                Filename = filename,
                AutoFlush = false,
                AutoShrink = false,
                InstantFlush = true
            });

            try
            {
                var messages = await database.Open();

                Assert.Equal(["ok-001", "ok-002"], messages.Select(x => x.MessageId).ToArray());
                Assert.Equal(["first", "second"], messages.Select(x => x.GetStringContent()).ToArray());
            }
            finally
            {
                await database.Close();
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, true);
            }
            catch
            {
            }
        }
    }

    private static HorseMessage CreateMessage(string id, string content)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "q");
        message.SetMessageId(id);
        message.SetStringContent(content);
        return message;
    }

    private static async Task WriteCorruptInsert(Stream stream, string id, uint declaredContentLength)
    {
        byte[] idBytes = Encoding.UTF8.GetBytes(id);

        stream.WriteByte((byte) DataType.Insert);
        stream.WriteByte((byte) idBytes.Length);
        await stream.WriteAsync(idBytes);

        await stream.WriteAsync(BitConverter.GetBytes(12));

        byte[] frame = new byte[12];
        frame[0] = (byte) MessageType.QueueMessage;
        frame[7] = 254;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), declaredContentLength);

        await stream.WriteAsync(frame);
    }
}
