using System.IO;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests.Complex.Subject
{
    /// <summary>
    /// UTF-8 в <see cref="MemoryStream"/>: тот же буфер, что у
    /// <see cref="Utf8StreamExhauster"/>. Отдельный тип нужен генератору.
    /// </summary>
    public class Utf8BinaryExhausterStream : Utf8StreamExhauster
    {
        public Utf8BinaryExhausterStream(MemoryStream stream)
            : base(stream)
        {
        }
    }
}
