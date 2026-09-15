using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using CompileRun = XmlSerDe.Tests.GeneratorHarness.CompileRun;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// Формы из тестового набора <c>System.Xml.Serialization</c>, которые нельзя
    /// положить в корпус <see cref="BclPortCorpus"/>: на них генератор либо
    /// отказывается работать, либо пишет код, который не компилируется, - и то
    /// и другое сломало бы сборку всего тестового проекта, а не покрасило
    /// один тест.
    ///
    /// Поэтому здесь генератор гоняется через <c>CSharpGeneratorDriver</c>,
    /// а его вывод компилируется отдельной сборкой - как в
    /// <see cref="GeneratedShapeCompileFixture"/>, у которого эта обвязка
    /// и заимствована.
    ///
    /// Различать надо две вещи, и тесты ниже разведены именно по этому признаку:
    /// <list type="bullet">
    /// <item><b>отказ</b> - генератор объясняет диагностикой, чего он не умеет.
    /// Это законное поведение: потребитель видит сообщение, а в drop-in-режиме
    /// тип просто уходит штатным путём;</item>
    /// <item><b>несобираемый вывод</b> - генератор берётся за форму и пишет
    /// код с ошибкой компиляции. Это дефект, и такой тест красный до починки.</item>
    /// </list>
    /// </summary>
    public class BclGeneratedShapeFixture
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using XmlSerDe;
using XmlSerDe.Internal;
";

        #region отказы: генератор объясняет, чего не умеет

        /// <summary>
        /// <c>XML_TypeWithArrayLikeFieldsOrdered</c>: <c>[XmlElement("num")]</c>
        /// на массиве - это элементы без обёртки, подряд. XmlSerDe такую форму
        /// не умеет, и отказ здесь законен: сообщение называет член и предлагает
        /// <c>[XmlArray]</c>.
        ///
        /// Красное в этом тесте - последнее утверждение, про стектрейс.
        /// <c>XmlDeserializeGenerator.Build</c> сам постановил правило:
        /// "у отказа с названной причиной стектрейс только мешает: читать его
        /// некому, а сообщение он топит", - и отличает такие отказы по типу
        /// <c>GenerationRefusedException</c>. Этот отказ брошен обычным
        /// исключением, поэтому к внятному сообщению приклеен стек на девять
        /// кадров, и в выводе сборки видно его, а не совет про <c>[XmlArray]</c>.
        /// </summary>
        [Fact]
        public void XmlElementOnArrayMember_IsRefusedWithDiagnostic()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class TypeWithArrayLikeFieldsOrdered
    {
        [XmlElement(Order = 3, ElementName = ""strfld"")]
        public string StringField2;
        [XmlElement(Order = 1, ElementName = ""num"")]
        public int[] Numbers;
        [XmlElement(Order = 0)]
        public int Leading;
        [XmlElement(Order = 2, ElementName = ""strfld"")]
        public string StringField1;
    }

    [XmlSubject(typeof(TypeWithArrayLikeFieldsOrdered), true)]
    public partial class Host { }
}
");

            AssertRefusedNaming(run, "Numbers");
        }

        /// <summary>
        /// <c>Xml_TestTypeWithListPropertiesWithoutPublicSetters</c>: тот же отказ,
        /// но на <c>List&lt;T&gt;</c> и на свойстве, а не на массиве и поле.
        /// </summary>
        [Fact]
        public void XmlElementOnListMember_IsRefusedWithDiagnostic()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class TypeWithListPropertiesWithoutPublicSetters
    {
        [XmlElement(""PropWithXmlElementAttr"")]
        public List<string> PropertyWithXmlElementAttribute { get; private set; }
    }

    [XmlSubject(typeof(TypeWithListPropertiesWithoutPublicSetters), true)]
    public partial class Host { }
}
");

            AssertRefusedNaming(run, "PropertyWithXmlElementAttribute");
        }

        #endregion

        #region [DefaultValue]: значения, которые не ложатся в константу исходника

        /// <summary>
        /// <c>Xml_DefaultValueAttributeSetToNaNTest</c>. <c>double.NaN</c>
        /// в <c>[DefaultValue]</c> приходит к генератору боксированным
        /// <c>double</c>, и его <c>ToString()</c> даёт <c>NaN</c> - текст,
        /// который в C# не константа, а неизвестное имя.
        ///
        /// Форма законная и BCL её обслуживает (умолчание, не равное самому себе,
        /// просто никогда не совпадает, и члены пишутся всегда), поэтому отказом
        /// это быть не может - только сборкой.
        /// </summary>
        [Fact]
        public void NaNDefaultValue_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
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

    [XmlSubject(typeof(DefaultValuesSetToNaN), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// <c>Xml_DefaultValueAttributeSetToPositiveInfinityTest</c> и
        /// <c>...NegativeInfinity...</c>: то же самое, но <c>ToString()</c>
        /// даёт <c>Infinity</c> и <c>-Infinity</c>.
        /// </summary>
        [Fact]
        public void InfinityDefaultValue_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class DefaultValuesSetToInfinity
    {
        [DefaultValue(double.PositiveInfinity)]
        public double Positive { get; set; }
        [DefaultValue(double.NegativeInfinity)]
        public double Negative { get; set; }
        [DefaultValue(float.PositiveInfinity)]
        public float PositiveSingle;
    }

    [XmlSubject(typeof(DefaultValuesSetToInfinity), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// <c>Xml_TypeWithEnumPropertyHavingDefaultValue</c>: <c>[DefaultValue(1)]</c>
        /// при члене типа перечисления. В атрибуте лежит <c>int</c>, а сравнивать
        /// его предстоит с перечислением, и <c>x != 1</c> для перечисления -
        /// ошибка компиляции (неявное приведение есть только у литерала 0).
        /// </summary>
        [Fact]
        public void IntDefaultValueOnEnumMember_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public enum IntEnum { Option0, Option1, Option2 }

    public class TypeWithEnumPropertyHavingDefaultValue
    {
        [DefaultValue(1)]
        public IntEnum EnumProperty { get; set; } = IntEnum.Option1;
    }

    [XmlSubject(typeof(TypeWithEnumPropertyHavingDefaultValue), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// <c>Xml_TypeWithMismatchBetweenAttributeAndPropertyType</c>:
        /// <c>[DefaultValue(true)]</c> при члене типа <c>int</c>. У BCL это
        /// просто несопоставимое умолчание - атрибут пишется всегда; у нас
        /// сравнение <c>int != true</c> не компилируется.
        /// </summary>
        [Fact]
        public void MismatchedDefaultValueType_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    [XmlRoot(""RootElement"")]
    public class TypeWithMismatchBetweenAttributeAndPropertyType
    {
        [DefaultValue(true)]
        [XmlAttribute(""IntValue"")]
        public int IntValue { get; set; } = 120;
    }

    [XmlSubject(typeof(TypeWithMismatchBetweenAttributeAndPropertyType), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// <c>Xml_TypeWithDefaultTimeSpanProperty</c>: конструктор
        /// <c>[DefaultValue(typeof(TimeSpan), "00:01:00")]</c> - единственная
        /// форма, которой можно задать умолчание для типа без константного
        /// литерала.
        /// </summary>
        [Fact]
        public void TypeAndStringDefaultValue_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class TypeWithDefaultTimeSpanProperty
    {
        [DefaultValue(typeof(TimeSpan), ""00:01:00"")]
        public TimeSpan TimeSpanProperty { get; set; }
        [DefaultValue(typeof(TimeSpan), ""00:00:01"")]
        public TimeSpan TimeSpanProperty2 { get; set; }
    }

    [XmlSubject(typeof(TypeWithDefaultTimeSpanProperty), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        #endregion

        #region обвязка

        /// <summary>
        /// Отказ обязан быть диагностикой с именем члена и без стектрейса -
        /// по правилу, которое генератор объявил у себя в <c>Build</c>
        /// (<c>GenerationRefusedException</c> против всего остального).
        /// </summary>
        private static void AssertRefusedNaming(CompileRun run, string memberName)
        {
            var errors = run.GeneratorDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.True(
                errors.Count > 0,
                "ожидалась диагностика отказа, а генератор промолчал"
                );

            Assert.True(
                errors.Any(d => d.GetMessage().Contains(memberName, StringComparison.Ordinal)),
                $"ни одна диагностика не называет член {memberName}:" + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Select(d => d.ToString()))
                );

            Assert.True(
                //имя типа из стека, а не слово "at": на .NET Framework кадры
                //локализованы ("в XmlSerDe.Generator..."), и проверка по "at"
                //молча проходила бы на одном таргете из трёх
                errors.All(d => !d.GetMessage().Contains("XmlSerDe.Generator.Producer.ClassSourceProducer", StringComparison.Ordinal)),
                "к сообщению об отказе приклеен стектрейс - значит оно брошено обычным "
                    + "исключением, а не GenerationRefusedException:" + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Select(d => d.ToString()))
                );
        }

        private static void AssertCompiles(CompileRun run)
        {
            Assert.True(
                run.GeneratorDiagnostics.All(d => d.Severity != DiagnosticSeverity.Error),
                "generator errors:" + Environment.NewLine + string.Join(Environment.NewLine, run.GeneratorDiagnostics.Select(d => d.ToString()))
                );
            Assert.True(
                run.CompileErrors.Count == 0,
                "compile errors:" + Environment.NewLine + string.Join(Environment.NewLine, run.CompileErrors.Select(d => d.ToString()))
                );
        }

        private static CompileRun Compile(string source)
        {
            return GeneratorHarness.Compile(source, "BclGeneratedShapeFixtureAssembly");
        }

        #endregion
    }
}
