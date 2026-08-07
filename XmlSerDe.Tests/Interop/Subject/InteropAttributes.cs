#nullable disable

using System.ComponentModel;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Interop.Subject
{
    public enum InteropEnum
    {
        Zero = 0,
        One = 1,
        Two = 2,
    }

    /// <summary>
    /// Перечисление с переименованными через <see cref="XmlEnumAttribute"/> членами.
    /// </summary>
    public enum RenamedEnum
    {
        [XmlEnum("zero-value")]
        Zero = 0,

        [XmlEnum("one-value")]
        One = 1,
    }

    /// <summary>
    /// Обычное перечисление - член и элемент коллекции.
    /// </summary>
    public class EnumSubject
    {
        public InteropEnum Value { get; set; }
        public InteropEnum Other { get; set; }
    }

    /// <summary>
    /// <see cref="XmlEnumAttribute"/>: имя в XML отличается от имени члена C#.
    /// </summary>
    public class RenamedEnumSubject
    {
        public RenamedEnum Value { get; set; }
    }

    /// <summary>
    /// <see cref="XmlIgnoreAttribute"/> - единственный атрибут BCL, который XmlSerDe
    /// уже понимает как есть.
    /// </summary>
    public class IgnoreSubject
    {
        public int Kept { get; set; }

        [XmlIgnore]
        public int Skipped { get; set; }
    }

    /// <summary>
    /// <see cref="XmlElementAttribute"/> с переименованием элемента.
    /// </summary>
    public class RenamedElementSubject
    {
        [XmlElement("renamed")]
        public int Value { get; set; }

        public int Untouched { get; set; }
    }

    /// <summary>
    /// <see cref="XmlAttributeAttribute"/>: член уходит в атрибут головы, а не в дочерний элемент.
    /// </summary>
    public class AttributeSubject
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string Payload { get; set; }
    }

    /// <summary>
    /// <see cref="XmlRootAttribute"/>: имя корневого элемента отличается от имени типа.
    /// </summary>
    [XmlRoot("renamed-root")]
    public class RootRenamedSubject
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// <see cref="XmlTypeAttribute"/>: имя типа в XML отличается от имени типа C#.
    /// Влияет и на корень, и на xsi:type.
    /// </summary>
    [XmlType("renamed-type")]
    public class TypeRenamedSubject
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// <see cref="XmlArrayAttribute"/> и <see cref="XmlArrayItemAttribute"/>: обёртка
    /// коллекции и её элементы названы вручную.
    /// </summary>
    public class RenamedArraySubject
    {
        [XmlArray("items")]
        [XmlArrayItem("item")]
        public int[] Values { get; set; }
    }

    /// <summary>
    /// <see cref="XmlTextAttribute"/>: содержимое члена становится текстом самого элемента.
    /// </summary>
    public class TextSubject
    {
        [XmlText]
        public string Text { get; set; }
    }

    /// <summary>
    /// Паттерн XxxSpecified: BCL считает такой bool служебным и не пишет его,
    /// а сам член пишет только если он true.
    /// </summary>
    public class SpecifiedSubject
    {
        public int Value { get; set; }

        [XmlIgnore]
        public bool ValueSpecified { get; set; }

        public int Always { get; set; }
    }

    /// <summary>
    /// <see cref="DefaultValueAttribute"/>: BCL не пишет член, равный значению по умолчанию.
    /// </summary>
    public class DefaultValueSubject
    {
        [DefaultValue(42)]
        public int Value { get; set; }

        public int Other { get; set; }
    }

    /// <summary>
    /// <see cref="XmlElementAttribute.Order"/>: порядок элементов задан явно и не совпадает
    /// с порядком объявления в C#.
    /// </summary>
    public class OrderedSubject
    {
        [XmlElement(Order = 2)]
        public int First { get; set; }

        [XmlElement(Order = 1)]
        public int Second { get; set; }
    }
}
