using System.Xml;
using System.Xml.Serialization;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Парный к <see cref="XmlSerDeSerializationWriter"/> пустой
    /// <see cref="XmlSerializationReader"/>: отдаёт <see cref="XmlReader"/>,
    /// на котором стоит вызывающий.
    /// </summary>
    internal sealed class XmlSerDeSerializationReader : XmlSerializationReader
    {
        public XmlReader Target => Reader;

        protected override void InitCallbacks()
        {
        }

        protected override void InitIDs()
        {
        }
    }
}
