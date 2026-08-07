#nullable disable

using System;

namespace XmlSerDe.Tests.Interop.Subject
{
    /// <summary>
    /// Все примитивы, которые XmlSerDe объявляет встроенными. float/double/char/TimeSpan
    /// сюда не входят намеренно: генератор их не знает и падает с ошибкой компиляции,
    /// поэтому форму с ними нельзя даже собрать - это расхождение видно не тестом, а сборкой.
    /// </summary>
    public class ScalarsSubject
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
        public string StringMember { get; set; }
        public Guid GuidMember { get; set; }
        public DateTime DateTimeMember { get; set; }
    }

    /// <summary>
    /// Nullable-значения: и заполненные, и пустые. Обе стороны заявляют, что null-член
    /// не пишется вовсе, а не пишется пустым тегом.
    /// </summary>
    public class NullableSubject
    {
        public int? FilledInt { get; set; }
        public int? EmptyInt { get; set; }
        public bool? FilledBool { get; set; }
        public bool? EmptyBool { get; set; }
        public decimal? FilledDecimal { get; set; }
        public decimal? EmptyDecimal { get; set; }
        public Guid? FilledGuid { get; set; }
        public Guid? EmptyGuid { get; set; }
        public DateTime? FilledDateTime { get; set; }
        public DateTime? EmptyDateTime { get; set; }
    }

    /// <summary>
    /// Строки во всех состояниях, которые различают сериализаторы: null (член пропускается),
    /// пустая (пустой тег), с символами, требующими экранирования, и с пробелами по краям.
    /// </summary>
    public class StringsSubject
    {
        public string Ordinary { get; set; }
        public string Empty { get; set; }
        public string Null { get; set; }
        public string NeedsEscaping { get; set; }
        public string LeadingAndTrailingSpaces { get; set; }
        public string WithNewLines { get; set; }
    }

    /// <summary>
    /// DateTime во всех трёх Kind: у каждого своя лексическая форма, и расходятся они порознь.
    /// </summary>
    public class DateTimeKindsSubject
    {
        public DateTime Utc { get; set; }
        public DateTime Local { get; set; }
        public DateTime Unspecified { get; set; }
        public DateTime WholeSecond { get; set; }
    }

    /// <summary>
    /// Публичные поля: обе стороны их сериализуют наравне со свойствами.
    /// </summary>
    public class FieldsSubject
    {
        public int IntField;
        public string StringField;
        public int IntProperty { get; set; }
    }

    /// <summary>
    /// Тип без единого члена. Вырожденный случай, на котором разъезжается
    /// "пустой тег" против "пары тегов".
    /// </summary>
    public class EmptySubject
    {
    }
}
