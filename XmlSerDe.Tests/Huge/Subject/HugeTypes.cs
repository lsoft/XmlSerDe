#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Huge.Subject
{
    /// <summary>
    /// Корневой документ HUGE-сценария: широкий граф со всеми типами, которые
    /// XmlSerDe умеет писать и читать, повторённый столько раз, сколько нужно,
    /// чтобы XML добрался до целевого размера.
    /// </summary>
    public class HugeDocument
    {
        [XmlAttribute("id")]
        public Guid Id { get; set; }

        public string Title { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<HugeRecord> Records { get; set; }
    }

    public enum HugeKind
    {
        Alpha = 0,
        Beta = 1,
        Gamma = 2,
        Delta = 3,
    }

    public enum HugeRenamedKind
    {
        [XmlEnum("zero-value")]
        Zero = 0,

        [XmlEnum("one-value")]
        One = 1,
    }

    /// <summary>
    /// Одна запись корпуса. Поля намеренно разнородные: скаляры, nullable,
    /// коллекции, полиморфизм, атрибуты, бинарь, вложенность. Какие из них
    /// заполнены, а какие остались null, выбирает <c>index</c> в билдере -
    /// так в одном документе оказываются все ветки, а не одна «счастливая».
    /// </summary>
    public class HugeRecord
    {
        [XmlAttribute("n")]
        public int Index { get; set; }

        [XmlAttribute("kind")]
        public HugeKind Kind { get; set; }

        public HugeScalars Scalars { get; set; }

        public HugeNullables Nullables { get; set; }

        public HugeStrings Strings { get; set; }

        public HugeNode Tree { get; set; }

        public List<int> Numbers { get; set; }

        public int[] NumberArray { get; set; }

        public List<string> Labels { get; set; }

        public string[] LabelArray { get; set; }

        public List<HugeChild> Children { get; set; }

        public HugeChild[] ChildArray { get; set; }

        public List<int> EmptyOrNullList { get; set; }

        public int[] EmptyOrNullArray { get; set; }

        public HugeShape Shape { get; set; }

        public List<HugeShape> Shapes { get; set; }

        public HugeBase MaybeBase { get; set; }

        public HugeDerived Derived { get; set; }

        public byte[] Blob { get; set; }

        public byte[] EmptyBlob { get; set; }

        public List<byte> ByteList { get; set; }

        [XmlArray("raw-bytes")]
        [XmlArrayItem("b")]
        public byte[] WrappedBytes { get; set; }

        public HugeTextPayload TextPayload { get; set; }

        public HugeOrdered Ordered { get; set; }

        public HugeEmpty EmptyMarker { get; set; }

        public HugeKind EnumValue { get; set; }

        public HugeRenamedKind RenamedKind { get; set; }

        public List<HugeKind> Enums { get; set; }

        public TimeSpan Duration { get; set; }

        public TimeSpan? OptionalDuration { get; set; }

        public DateTime UtcTime { get; set; }

        public DateTime LocalTime { get; set; }

        public DateTime UnspecifiedTime { get; set; }

        public int FieldNumber;

        public string FieldLabel;

        [XmlIgnore]
        public int Ignored { get; set; }

        public int OptionalNumber { get; set; }

        [XmlIgnore]
        public bool OptionalNumberSpecified { get; set; }

        public HugeChild MissingChild { get; set; }

        [XmlElement("renamed-score")]
        public double Score { get; set; }

        [DefaultValue(0)]
        public int Defaulted { get; set; }
    }

    public class HugeScalars
    {
        public bool BoolMember { get; set; }
        public sbyte SByteMember { get; set; }
        public byte ByteMember { get; set; }
        public short ShortMember { get; set; }
        public ushort UShortMember { get; set; }
        public int IntMember { get; set; }
        public uint UIntMember { get; set; }
        public long LongMember { get; set; }
        public ulong ULongMember { get; set; }
        public decimal DecimalMember { get; set; }
        public float FloatMember { get; set; }
        public double DoubleMember { get; set; }
        public char CharMember { get; set; }
        public string StringMember { get; set; }
        public Guid GuidMember { get; set; }
        public DateTime DateTimeMember { get; set; }
        public TimeSpan TimeSpanMember { get; set; }
    }

    public class HugeNullables
    {
        public bool? BoolMember { get; set; }
        public sbyte? SByteMember { get; set; }
        public byte? ByteMember { get; set; }
        public short? ShortMember { get; set; }
        public ushort? UShortMember { get; set; }
        public int? IntMember { get; set; }
        public uint? UIntMember { get; set; }
        public long? LongMember { get; set; }
        public ulong? ULongMember { get; set; }
        public decimal? DecimalMember { get; set; }
        public float? FloatMember { get; set; }
        public double? DoubleMember { get; set; }
        public char? CharMember { get; set; }
        public Guid? GuidMember { get; set; }
        public DateTime? DateTimeMember { get; set; }
        public TimeSpan? TimeSpanMember { get; set; }
    }

    public class HugeStrings
    {
        public string Ordinary { get; set; }
        public string Empty { get; set; }
        public string Null { get; set; }
        public string NeedsEscaping { get; set; }
        public string LeadingAndTrailingSpaces { get; set; }
        public string WithNewLines { get; set; }
        public string LongText { get; set; }
    }

    public class HugeNode
    {
        [XmlAttribute("d")]
        public int Depth { get; set; }

        public string Label { get; set; }

        public HugeNode Child { get; set; }

        public List<HugeNode> Branches { get; set; }

        public string Payload { get; set; }
    }

    public class HugeChild
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string Name { get; set; }

        public int Number { get; set; }

        public HugeChild Nested { get; set; }
    }

    [XmlInclude(typeof(HugeCircle))]
    [XmlInclude(typeof(HugeRectangle))]
    public abstract class HugeShape
    {
        public string Color { get; set; }
    }

    public class HugeCircle : HugeShape
    {
        public double Radius { get; set; }
    }

    public class HugeRectangle : HugeShape
    {
        public double Width { get; set; }

        public double Height { get; set; }
    }

    [XmlInclude(typeof(HugeDerived))]
    public class HugeBase
    {
        public int BaseNumber { get; set; }

        public string BaseName { get; set; }
    }

    public class HugeDerived : HugeBase
    {
        public int DerivedNumber { get; set; }

        public string DerivedName { get; set; }
    }

    public class HugeTextPayload
    {
        [XmlAttribute("a")]
        public string Attribute { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    public class HugeOrdered
    {
        [XmlElement(Order = 2)]
        public int First { get; set; }

        [XmlElement(Order = 1)]
        public int Second { get; set; }
    }

    public class HugeEmpty
    {
    }
}
