using System.IO;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests.Complex.Subject
{
    /// <summary>
    /// Utf8BinaryExhauster that writes to a MemoryStream for round-trip tests.
    /// </summary>
    public class Utf8BinaryExhausterStream : Utf8BinaryExhauster
    {
        private readonly MemoryStream _stream;

        public Utf8BinaryExhausterStream(MemoryStream stream)
        {
            _stream = stream ?? throw new System.ArgumentNullException(nameof(stream));
        }

        protected override void Write(byte[] data, int length)
        {
            _stream.Write(data, 0, length);
        }
    }
}
