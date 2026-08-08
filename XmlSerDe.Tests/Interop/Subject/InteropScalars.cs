#nullable disable

using System;

namespace XmlSerDe.Tests.Interop.Subject
{
    /// <summary>
    /// Целочисленные и прочие примитивы с однозначной лексической формой.
    /// Вещественные, char и TimeSpan вынесены в <see cref="TrickyScalarsSubject"/>:
    /// у них форма как раз неочевидна.
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
    /// Примитивы, у которых лексическая форма не выводится из типа:
    ///
    /// <list type="bullet">
    /// <item>вещественные - кратчайшее round-trippable представление, но бесконечности
    /// пишутся как <c>INF</c>/<c>-INF</c>, а не "Infinity";</item>
    /// <item><see cref="char"/> - кодовой точкой числом, потому что типа для одиночного
    /// символа в xsd нет вовсе.</item>
    /// </list>
    ///
    /// Пока генератор их не знал, форму с ними нельзя было даже собрать - расхождение
    /// было видно сборкой, а не тестом. Теперь оно видно тестом.
    /// </summary>
    public class TrickyScalarsSubject
    {
        public float FloatMember { get; set; }
        public double DoubleMember { get; set; }
        public double ThirdMember { get; set; }
        public float NaNMember { get; set; }
        public double PositiveInfinityMember { get; set; }
        public double NegativeInfinityMember { get; set; }
        public char CharMember { get; set; }
        public char NonAsciiCharMember { get; set; }
        public double? FilledNullableDouble { get; set; }
        public double? EmptyNullableDouble { get; set; }
    }

    /// <summary>
    /// <see cref="TimeSpan"/> отдельной формой, потому что он единственный расходится
    /// не с BCL вообще, а с BCL на конкретном таргете: поддержку <see cref="TimeSpan"/>
    /// в <c>XmlSerializer</c> завезли только в .NET Core. На .NET Framework у него нет
    /// ни одного публичного члена с сеттером, поэтому BCL пишет пустой элемент и теряет
    /// значение целиком - см. <c>InteropFixture.Durations_Test</c>.
    /// </summary>
    public class DurationSubject
    {
        public TimeSpan Ordinary { get; set; }
        public TimeSpan Zero { get; set; }
        public TimeSpan Negative { get; set; }
        public TimeSpan? Filled { get; set; }
        public TimeSpan? Empty { get; set; }
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
