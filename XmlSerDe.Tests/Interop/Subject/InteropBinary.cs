#nullable disable

using System.Collections.Generic;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Interop.Subject
{
    /// <summary>
    /// <c>byte[]</c> - единственная коллекция, которую System.Xml.Serialization пишет
    /// не коллекцией: в документе это одна лексема base64Binary. Здесь же и пустой
    /// массив, и null - у них разная судьба: пустой становится <c>&lt;Empty/&gt;</c>,
    /// а null не пишется вовсе, и обратно они читаются в <c>byte[0]</c> и в null.
    /// </summary>
    public class BinarySubject
    {
        public byte[] Bytes { get; set; }
        public byte[] Empty { get; set; }
        public byte[] Missing { get; set; }
        public int After { get; set; }
    }

    /// <summary>
    /// <c>byte[]</c> в значении атрибута - тоже base64, хотя всякая другая коллекция
    /// в атрибут не пролезает вовсе.
    /// </summary>
    public class BinaryAttributeSubject
    {
        [XmlAttribute("b")]
        public byte[] Bytes { get; set; }
    }

    /// <summary>
    /// <c>byte[]</c> телом собственного элемента.
    /// </summary>
    public class BinaryTextSubject
    {
        [XmlText]
        public byte[] Bytes { get; set; }
    }

    /// <summary>
    /// Явная обёртка отменяет base64 и возвращает массиву вид обычной коллекции:
    /// <c>&lt;wrap&gt;&lt;item&gt;1&lt;/item&gt;…&lt;/wrap&gt;</c>. Правило принадлежит
    /// не типу, а паре "член плюс его атрибуты", и это единственное место в корпусе,
    /// где один и тот же <c>byte[]</c> пишется двумя разными способами.
    /// </summary>
    public class BinaryArraySubject
    {
        [XmlArray("wrap")]
        [XmlArrayItem("item")]
        public byte[] Bytes { get; set; }
    }

    /// <summary>
    /// <c>List&lt;byte&gt;</c> под base64 не подпадает: это обычная коллекция
    /// <c>&lt;unsignedByte&gt;</c>. Форма заведена именно затем, чтобы перехват
    /// <c>byte[]</c> не расползся на всё, что состоит из байтов.
    /// </summary>
    public class ByteListSubject
    {
        public List<byte> Bytes { get; set; }
    }
}
