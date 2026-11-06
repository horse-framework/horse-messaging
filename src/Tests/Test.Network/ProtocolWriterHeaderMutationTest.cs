using System.Collections;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

public class ProtocolWriterHeaderMutationTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_HeadersShrinkBetweenSizingAndWriting_RejectsInconsistentFrame(bool removeAll)
    {
        var message = new HorseMessage(MessageType.Server);
        var headers = new ShrinkingHeaders(removeAll);

        Assert.Throws<InvalidOperationException>(() => HorseProtocolWriter.Create(message, headers));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_HeadersShrinkBetweenSizingAndWriting_DoesNotEmitUnwrittenPooledBytes(bool removeAll)
    {
        var message = new HorseMessage(MessageType.Server);
        var headers = new ShrinkingHeaders(removeAll);
        using var output = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() => HorseProtocolWriter.Write(message, output, headers));

        // The 2004-byte header takes the pooled path. A failed write may already
        // have emitted its frame, but must emit no header bytes or pooled tail.
        Assert.Equal(new byte[] { 48, 0, 0, 0, 0, 0, 0, 0 }, output.ToArray());
    }

    /// <summary>
    /// Simulates a mutable IList changing between the size and write passes,
    /// without a timing-dependent background thread. The first indexed read
    /// exposes a large value; later reads expose one character or no entries.
    /// </summary>
    private sealed class ShrinkingHeaders(bool removeAll) : IList<KeyValuePair<string, string>>
    {
        private int _reads;

        public KeyValuePair<string, string> this[int index]
        {
            get => new("k", _reads++ == 0 ? new string('v', 2000) : "v");
            set => throw new NotSupportedException();
        }

        public int Count => removeAll && _reads > 0 ? 0 : 1;
        public bool IsReadOnly => true;

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();
        public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public void Insert(int index, KeyValuePair<string, string> item) => throw new NotSupportedException();
        public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}
