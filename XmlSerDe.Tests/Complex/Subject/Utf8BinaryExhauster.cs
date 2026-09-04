using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests.Complex.Subject
{
    /// <summary>
    /// UTF-8 без I/O: кодирование то же, <see cref="Write"/> только считает
    /// байты, чтобы результат нельзя было выкинуть как мёртвый код.
    /// </summary>
    public sealed class Utf8BinaryExhausterEmpty : Utf8BinaryExhauster
    {
        public int Written { get; private set; }

        protected override void Write(byte[] data, int length)
        {
            Written += length;
        }
    }
}
