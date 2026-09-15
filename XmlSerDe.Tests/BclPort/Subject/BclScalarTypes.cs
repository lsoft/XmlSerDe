#nullable disable

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.BclPort.Subject
{
    //
    // Формы POCO, перенесённые из тестового набора самого
    // System.Xml.Serialization (dotnet/runtime,
    // `src/libraries/System.Private.Xml/tests/XmlSerializer`, типы -
    // `src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTypes*.cs`).
    //
    // Имена типов и членов оставлены такими же, как у первоисточника: по ним
    // находится исходный тест, а вместе с ним - то, какое поведение BCL считает
    // правильным. Выкинуто только то, что в нашу область определения не входит
    // вовсе (SOAP, схемы, `IXmlSerializable`, пространства имён) или мешает
    // сравнению (методы `Equals`, `Random` в фабриках).
    //
    // Проверяются они не эталонным текстом из исходного теста, а тем же
    // дифференциальным прогоном, что и корпус `Interop`: эталонный XML там
    // записан с отступами и с `xmlns:xsi`/`xmlns:xsd` на корне, а
    // XmlSerDe оба этих различия допускает намеренно (README, "Where the output
    // differs"). Сверять с текстом значило бы красить всё подряд одним и тем же
    // известным расхождением; сверка с живым `XmlSerializer` на том же
    // объекте отвечает на вопрос, который тест на самом деле задаёт.
    //

    /// <summary>
    /// <c>Xml_SimpleType</c>. Самая простая форма в наборе; держится здесь как
    /// нулевая отметка - если красная она, то дело не в переносимой особенности.
    /// </summary>
    public class SimpleType
    {
        public string P1 { get; set; }
        public int P2 { get; set; }
    }

    /// <summary>
    /// <c>Xml_TypeWithByteProperty</c> и <c>Xml_DeserializeOutOfRangeByteProperty</c>:
    /// <c>byte</c> полем, и отдельно - что значение вне диапазона на чтении
    /// не молчит, а падает.
    /// </summary>
    public class TypeWithByteProperty
    {
        public byte ByteProperty;
    }

    /// <summary>
    /// <c>Xml_TypeWithTimeSpanProperty</c>, <c>Xml_DeserializeTypeWithEmptyTimeSpanProperty</c>.
    /// Одна миллисекунда - значение, на котором видно и форму (<c>PT0.001S</c>),
    /// и то, что <c>&lt;TimeSpanProperty /&gt;</c> читается как <c>default</c>,
    /// а не как ошибка.
    /// </summary>
    public class TypeWithTimeSpanProperty
    {
        public TimeSpan TimeSpanProperty;
    }

    /// <summary>
    /// <c>Xml_TypeWithFieldNameEndBySpecified</c>. Пара <c>XxxSpecified</c> здесь
    /// не одна, а две, и вторая выключена: <c>MyFieldIgnored</c> со значением 99
    /// в документ попасть не должен вовсе, потому что
    /// <c>MyFieldIgnoredSpecified == false</c>.
    ///
    /// Наш <c>SpecifiedSubject</c> проверяет включённый случай; здесь проверяется
    /// выключенный, и заодно то, что сами <c>Specified</c>-члены помечены
    /// <c>[XmlIgnore]</c> и в документ не идут.
    /// </summary>
    public class TypeWithPropertyNameSpecified
    {
        public string MyField;

        [XmlIgnore]
        public bool MyFieldSpecified;

        public int MyFieldIgnored;

        [XmlIgnore]
        public bool MyFieldIgnoredSpecified;
    }

    /// <summary>
    /// <c>Xml_FieldBackedSpecifiedMember_SetOnDeserialize</c>: спутник -
    /// поле при свойстве, а не свойство при свойстве, и на чтении он обязан
    /// стать <c>true</c> от одного факта появления элемента.
    /// </summary>
    public class TypeWithFieldBackedSpecifiedMember
    {
        public string Foo { get; set; }

        [XmlIgnore]
        public bool FooSpecified;
    }

    /// <summary>
    /// <c>Xml_TypeWithPropertiesHavingDefaultValue_DefaultValue</c> и
    /// <c>..._NonDefaultValue</c>. Четыре <c>[DefaultValue]</c> разных типов,
    /// из которых у нас в корпусе был только <c>int</c>. Интересны пустая строка
    /// (значение по умолчанию - не null) и <c>char</c>: он пишется кодовой точкой,
    /// то есть сравнение с умолчанием идёт по значению, а не по тексту.
    /// </summary>
    public class TypeWithPropertiesHavingDefaultValue
    {
        [DefaultValue("")]
        public string EmptyStringProperty { get; set; } = "";

        [DefaultValue("DefaultString")]
        public string StringProperty { get; set; } = "DefaultString";

        [DefaultValue(11)]
        public int IntProperty { get; set; } = 11;

        [DefaultValue('m')]
        public char CharProperty { get; set; } = 'm';
    }

    /// <summary>
    /// <c>Xml_DefaultValueAttributeSetToNaNTest</c>. Умолчание, которое не равно
    /// самому себе: <c>NaN != NaN</c>, поэтому член со значением 0 обязан
    /// записаться, и оба члена - и поле, и свойство.
    ///
    /// Форма долго не могла попасть в корпус: <c>double.NaN</c> приходит
    /// к генератору боксированным <c>double</c>, и его <c>ToString()</c> давал
    /// в исходнике слово <c>NaN</c>, которого в C# нет (CS0103).
    /// </summary>
    public class DefaultValuesSetToNaN
    {
        [DefaultValue(double.NaN)]
        public double DoubleProp { get; set; }

        [DefaultValue(float.NaN)]
        public float FloatProp { get; set; }

        [DefaultValue(double.NaN)]
        public double DoubleField;

        [DefaultValue(float.NaN)]
        public float SingleField;
    }

    /// <summary>
    /// <c>Xml_DefaultValueAttributeSetToPositiveInfinityTest</c> и
    /// <c>SerializeWithDefaultValueSetToPositiveInfinityTest</c>: то же умолчание,
    /// но сравнимое. Значение 0 пишется, а выставленное в бесконечность -
    /// пропускается, и лексическая форма <c>INF</c> тут ни при чём, потому что
    /// в документ оно не попадает.
    /// </summary>
    public class DefaultValuesSetToPositiveInfinity
    {
        [DefaultValue(double.PositiveInfinity)]
        public double DoubleProp { get; set; }

        [DefaultValue(float.PositiveInfinity)]
        public float FloatProp { get; set; }

        [DefaultValue(double.PositiveInfinity)]
        public double DoubleField;

        [DefaultValue(float.PositiveInfinity)]
        public float SingleField;
    }

    public enum IntEnum
    {
        Option0,
        Option1,
        Option2,
    }

    /// <summary>
    /// <c>Xml_TypeWithEnumPropertyHavingDefaultValue</c>: <c>[DefaultValue(1)]</c>
    /// при члене-перечислении. В атрибуте лежит <c>int</c>, а сравнивать его надо
    /// с перечислением - и BCL именно так и делает: <c>Option1</c> он не пишет,
    /// <c>Option0</c> пишет. Исходный тест сверяет оба прогона, здесь они
    /// заведены отдельными формами корпуса.
    /// </summary>
    public class TypeWithEnumPropertyHavingDefaultValue
    {
        [DefaultValue(1)]
        public IntEnum EnumProperty { get; set; } = IntEnum.Option1;
    }

    /// <summary>
    /// <c>Xml_TypeWithShouldSerializeMethod_WithDefaultValue</c> /
    /// <c>..._WithNonDefaultValue</c>. Второй способ сказать "не пиши это
    /// значение", кроме <c>[DefaultValue]</c>: метод <c>ShouldSerializeXxx()</c>.
    /// README его не называет среди поддержанного, поэтому расхождение ожидается;
    /// тест заведён, чтобы оно было измерено, а не подразумевалось.
    /// </summary>
    public class TypeWithShouldSerializeMethod
    {
        private static readonly string DefaultFoo = "default";

        public string Foo { get; set; } = DefaultFoo;

        public bool ShouldSerializeFoo()
        {
            return Foo != DefaultFoo;
        }
    }

    /// <summary>
    /// <c>Xml_TypeWithNoSetters</c> (внутри <c>Xml_TestTypeWithPrivateOrNoSetters</c>):
    /// свойство без сеттера вообще. Обе стороны его пропускают, и на чтении
    /// значение остаётся тем, что поставил конструктор без параметров, - 200,
    /// а не записанные 25.
    /// </summary>
    public class TypeWithNoSetters
    {
        public TypeWithNoSetters()
            : this(200)
        {
        }

        public TypeWithNoSetters(int noSetter)
        {
            NoSetter = noSetter;
        }

        [XmlElement]
        public int NoSetter { get; }
    }

    /// <summary>
    /// <c>Xml_TypeWithSpecialCharacterInStringMember</c>: <c>&amp;</c> в значении.
    /// </summary>
    public class TypeA
    {
        public string Name;
    }

    /// <summary>
    /// <c>Xml_TypeNamesWithSpecialCharacters</c>. Имя типа и имя члена не-ASCII
    /// и с ведущими подчёркиваниями: проверяется, что имя элемента берётся
    /// из исходника как есть, без транслитерации и без экранирования.
    /// </summary>
    public class __TypeNameWithSpecialCharacters漢ñ
    {
        public string PropertyNameWithSpecialCharacters漢ñ { get; set; }
    }

    /// <summary>
    /// <c>Xml_StringWithNullChar</c>. У BCL это корневая строка, у нас корень
    /// обязан быть классом, поэтому символ переехал в член. Суть та же:
    /// <c>\0</c> - не <c>Char</c> по XML 1.0 §2.2, и обе стороны обязаны
    /// отказаться его записать и отказаться прочитать <c>&amp;#x0;</c>.
    /// </summary>
    public class StringWithNullCharSubject
    {
        public string Value { get; set; }
    }

    /// <summary>
    /// <c>Xml_TestIgnoreWhitespaceForDeserialization</c> (родом из приложения
    /// The Weather Channel). Значения приходят в CDATA, причём в одном случае
    /// секция окружена переводами строк и отступами, а в другом стоит вплотную
    /// к тегам. Значащими считаются только сами секции: отступы вокруг них
    /// в значение не попадают, а пробелы внутри - попадают.
    /// </summary>
    public class ServerSettings
    {
        public string DS2Root { get; set; }
        public string MetricConfigUrl { get; set; }
    }
}
