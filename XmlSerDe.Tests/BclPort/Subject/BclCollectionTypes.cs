#nullable disable

using System.Collections.Generic;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.BclPort.Subject
{
    /// <summary>
    /// <c>Xml_ArraylikeMembers</c> - самая плотная форма из перенесённых.
    /// Четыре разновидности коллекции (<c>int[]</c> и <c>List&lt;int&gt;</c>,
    /// каждая полем и свойством) прогоняются в трёх состояниях: заполненном,
    /// пустом и null, и состояния эти расходятся между собой.
    ///
    /// Комментарий исходного теста стоит того, чтобы его сюда перенести целиком:
    /// null-массив читается обратно как null, а null-коллекция - как пустая
    /// коллекция, и это не ошибка, а сохранённое ради совместимости с .NET
    /// Framework поведение. Плюс <c>[XmlArray(IsNullable = true)]</c>, от которого
    /// null пишется не пропуском элемента, а <c>xsi:nil="true"</c>.
    ///
    /// Аннотации <c>?</c> у первоисточника здесь сняты вместе с nullable-контекстом
    /// файла: на отбор членов они не влияют, а <c>IsNullable</c> задан атрибутом.
    /// </summary>
    public class TypeWithArraylikeMembers
    {
        public int[] IntAField;
        public int[] NIntAField;

        public List<int> IntLField;

        [XmlArray(IsNullable = true)]
        public List<int> NIntLField;

        public int[] IntAProp { get; set; }

        [XmlArray(IsNullable = true)]
        public int[] NIntAProp { get; set; }

        public List<int> IntLProp { get; set; }
        public List<int> NIntLProp { get; set; }

        public static TypeWithArraylikeMembers CreateWithPopulatedMembers() => new TypeWithArraylikeMembers
        {
            //у первоисточника здесь Random; для дифференциальной сверки нужен
            //воспроизводимый объект, поэтому значения прибиты, а разная длина
            //каждого члена - от первоисточника и сохранена
            IntAField = new[] { 1, 2, 3 },
            NIntAField = new[] { 4, 5 },
            IntLField = new List<int> { 6 },
            NIntLField = new List<int> { 7, 8 },
            IntAProp = new[] { 9, 10 },
            NIntAProp = new[] { 11, 12, 13 },
            IntLProp = new List<int> { 14, 15, 16 },
            NIntLProp = new List<int> { 17 },
        };

        public static TypeWithArraylikeMembers CreateWithEmptyMembers() => new TypeWithArraylikeMembers
        {
            IntAField = new int[] { },
            NIntAField = new int[] { },
            IntLField = new List<int>(),
            NIntLField = new List<int>(),
            IntAProp = new int[] { },
            NIntAProp = new int[] { },
            IntLProp = new List<int>(),
            NIntLProp = new List<int>(),
        };

        public static TypeWithArraylikeMembers CreateWithNullMembers() => new TypeWithArraylikeMembers();
    }

    /// <summary>
    /// <c>Xml_ArrayAsGetSet</c>: массивы сложного и примитивного типа, и полем,
    /// и свойством. Второй прогон того же теста - с null и пустыми массивами,
    /// где важно, что null-массив и на чтении остаётся null, а пустой остаётся
    /// пустым.
    /// </summary>
    public class TypeWithGetSetArrayMembers
    {
        public SimpleType[] F1;
        public int[] F2;

        public SimpleType[] P1 { get; set; }
        public int[] P2 { get; set; }
    }

    /// <summary>
    /// Выжимка из <c>Xml_PrimitiveArraysAndCollections</c>: оставлено то, что
    /// XmlSerDe заявляет поддержанным. Выкинуты <c>DateOnly</c>/<c>TimeOnly</c>
    /// (не во всех целевых фреймворках), <c>ArrayList</c> и
    /// <c>IntEnumerableCollection</c> (не <c>List&lt;T&gt;</c> и не массив),
    /// а также <c>[XmlArrayItem(DataType = "date")]</c> - <c>DataType</c> у нас
    /// не читается вовсе, и для него заведена отдельная форма.
    ///
    /// Остаются три вещи, которых в корпусе не было: массив <c>char</c> (каждый
    /// элемент - кодовая точка), массив перечисления и массив
    /// <c>Nullable&lt;int&gt;</c>, где пустой элемент пишется
    /// <c>xsi:nil="true"</c> внутри обёртки.
    /// </summary>
    public class PrimitiveCollections
    {
        public char[] Chars { get; set; }

        public List<int> Integers { get; set; }

        public int[] EmptyIntegers { get; set; }

        public PrimitiveCollectionEnum[] Enums { get; set; }

        public int?[] NullableIntegers { get; set; }
    }

    public enum PrimitiveCollectionEnum
    {
        One,
        Two,
    }

    /// <summary>
    /// <c>Xml_TestDeserializingUnknownNode</c>. Интересен не столько неизвестный
    /// узел, сколько текст члена <c>Description</c>: в документе он записан
    /// в несколько строк с отступами, и обе стороны обязаны отдать его как есть,
    /// вместе с переводами строк. Плюс перечисление и пустой элемент
    /// <c>&lt;ImagePath&gt;&lt;/ImagePath&gt;</c>, который читается пустой строкой,
    /// а не null.
    /// </summary>
    public class SerializableSlide
    {
        public SpotlightDescription Description { get; set; }
        public string DisplayCondition { get; set; }
        public string EventData { get; set; }
        public SlideEventType EventType { get; set; }
        public string ImageName { get; set; }
        public string ImagePath { get; set; }
        public string Title { get; set; }
    }

    public class SpotlightDescription
    {
        public bool IsDynamic { get; set; }
        public string Value { get; set; }
    }

    public enum SlideEventType
    {
        None,
        LaunchURL,
        LaunchSection,
        LaunchVideo,
        LaunchImage,
    }

    /// <summary>
    /// Корень-список из <c>Xml_TestDeserializingUnknownNode</c> у нас корнем быть
    /// не может (корень обязан быть классом), поэтому список переехал в член.
    /// Имя обёртки при этом становится именем члена, а у BCL корневым элементом
    /// был бы <c>ArrayOfSerializableSlide</c> - это разница постановки, а не
    /// поведения, и обе стороны сравниваются на одной и той же форме.
    /// </summary>
    public class SlideDeck
    {
        public List<SerializableSlide> Slides { get; set; }
    }

    /// <summary>
    /// <c>Xml_TypeWithNestedPublicType</c>: вложенный публичный тип. Имя элемента
    /// берётся короткое (<c>LevelData</c>), без имени объемлющего типа и без
    /// плюса из метаданных.
    ///
    /// Сам <c>TypeWithNestedPublicType</c> в корпус не идёт: у него
    /// <c>Level { get; private set; }</c>, а приватный сеттер штатный сериализатор
    /// не принимает вовсе (<c>Xml_TestTypeWithPrivateOrNoSetters</c>) - сверять
    /// было бы не с чем.
    /// </summary>
    public class TypeWithNestedPublicType
    {
        public class LevelData
        {
            public string Name { get; set; }
        }
    }

    /// <summary>
    /// Держатель для <see cref="TypeWithNestedPublicType.LevelData"/>: у BCL
    /// исходный тест сериализует <c>List&lt;LevelData&gt;</c> корнем.
    /// </summary>
    public class NestedTypeHolder
    {
        public List<TypeWithNestedPublicType.LevelData> Levels { get; set; }
    }
}
