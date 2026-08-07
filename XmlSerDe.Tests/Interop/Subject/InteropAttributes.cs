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
    ///
    /// <see cref="Note"/> оставлен null намеренно: значения "атрибута нет" в XML не
    /// существует, и BCL такой атрибут просто не пишет (проверено). А вот
    /// <see cref="Nullable{T}"/> в атрибуте BCL не допускает вовсе - падает на
    /// построении сериализатора, - поэтому его здесь и нет.
    /// </summary>
    public class AttributeSubject
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        //без имени: атрибут называется по имени члена
        [XmlAttribute]
        public string Tag { get; set; }

        [XmlAttribute("kind")]
        public RenamedEnum Kind { get; set; }

        [XmlAttribute("flag")]
        public bool Flag { get; set; }

        [XmlAttribute("note")]
        public string Note { get; set; }

        /// <summary>
        /// Значение атрибута нормализуется читателем: CR, LF и TAB он обязан
        /// заменить пробелом (XML 1.0 §3.3.3). Единственный, кто может это
        /// предотвратить, - пишущая сторона, числовой ссылкой.
        ///
        /// TAB здесь намеренно отсутствует: на нём BCL расходится сам с собой,
        /// см. <see cref="AttributeTabSubject"/>.
        /// </summary>
        [XmlAttribute("esc")]
        public string NeedsEscaping { get; set; }

        public string Payload { get; set; }
    }

    /// <summary>
    /// TAB в значении атрибута - единственное место, где BCL расходится сам с собой
    /// по таргетам. На .NET он пишет <c>&amp;#x9;</c> и переживает round-trip, на
    /// .NET Framework оставляет символ как есть, и читатель по XML 1.0 §3.3.3
    /// обязан заменить его пробелом. XmlSerDe пишет ссылку на всех таргетах.
    /// </summary>
    public class AttributeTabSubject
    {
        [XmlAttribute("t")]
        public string Tabbed { get; set; }
    }

    /// <summary>
    /// Атрибуты и текст на полиморфном члене: и то, и другое пишется в ту же голову,
    /// где уже стоит xsi:type, причём атрибуты базы идут раньше атрибутов наследника.
    /// </summary>
    [XmlInclude(typeof(AttributeDerived))]
    public class AttributeBase
    {
        [XmlAttribute("ba")]
        public int BaseAttribute { get; set; }
    }

    public class AttributeDerived : AttributeBase
    {
        [XmlAttribute("da")]
        public int DerivedAttribute { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    public class AttributeHolder
    {
        public AttributeBase Item { get; set; }
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
    /// Вместе с атрибутом - потому что это единственные два места, куда член может
    /// уехать из своего тега, и уживаться друг с другом они обязаны.
    /// </summary>
    public class TextSubject
    {
        [XmlAttribute("a")]
        public string Attribute { get; set; }

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
