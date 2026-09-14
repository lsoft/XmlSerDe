using System;
using System.IO;
using XmlSerDe;

namespace XmlSerDe.Tests.Complex.Subject
{
    /// <summary>
    /// UTF-8 прямо в <see cref="MemoryStream"/>. Отдельный тип нужен генератору;
    /// от <see cref="Utf8StreamExhauster"/> не наследуется, потому что тот запечатан -
    /// иначе сгенерированный вызов не девиртуализуется (XMLSERDE010).
    /// </summary>
    public sealed class Utf8BinaryExhausterStream : Utf8BinaryExhauster, IDisposable
    {
        private readonly MemoryStream _stream;

        public Utf8BinaryExhausterStream(MemoryStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        protected override void Write(byte[] data, int length)
        {
            _stream.Write(data, 0, length);
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
