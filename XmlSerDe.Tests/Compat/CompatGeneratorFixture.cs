#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Отказ фасада проверяется здесь, на уровне самого генератора, а не по
    /// поведению собранного кода: <see cref="CompatFixture"/> видит только «тип
    /// не ускорен», а вопрос «почему» - это диагностика, и она либо есть, либо нет.
    ///
    /// Дисциплина, которую здесь и караулят: <b>отказ должен быть отказом</b>.
    /// Неподдержанный тип обязан уйти штатному сериализатору целиком и с внятной
    /// причиной, а не получить почти правильный сгенерированный код.
    /// </summary>
    public class CompatGeneratorFixture
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;
";

        [Fact]
        public void SupportedType_IsAccelerated_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
    public string Name { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
            Assert.Contains("PooledCharExhauster", result.Registration);
            Assert.Contains("LengthEstimatorExhauster", result.Registration);
            Assert.Contains("Utf8StreamExhauster", result.Registration);
            Assert.DoesNotContain("StringBuilderExhauster", result.Registration);
        }

        /// <summary>
        /// Фасад не подключён к проекту вовсе - генератор про совместимость
        /// не порождает ни строки. Это и есть «не используешь - не платишь»,
        /// проверенное, а не обещанное.
        /// </summary>
        [Fact]
        public void WithoutFacade_NothingIsGenerated_Test()
        {
            var result = Run(@"
using System.Xml.Serialization;

public class Ok
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        /// <summary>
        /// <c>typeof</c> обязан быть литеральным: по переменной тип на этапе
        /// сборки неизвестен, и такой вызов вообще не попадает в поле зрения.
        /// </summary>
        [Fact]
        public void NonLiteralTypeof_IsNotACallSite_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make(Type t) => new XmlSerializer(t);
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        [Fact]
        public void UnsupportedMember_RefusesWholeType_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var refusal = Assert.Single(result.CompatRefusals);
            Assert.Equal(DiagnosticSeverity.Info, refusal.Severity);
            Assert.Contains("global::Bad", refusal.GetMessage());
            Assert.Contains("Set", refusal.GetMessage());
        }

        /// <summary>
        /// Отказ одного типа не должен лишать ускорения остальные: они друг
        /// о друге ничего не знают.
        /// </summary>
        [Fact]
        public void RefusalOfOneRoot_LeavesOthersAccelerated_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
}

public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object MakeOk() => new XmlSerializer(typeof(Ok));
    public static object MakeBad() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Single(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
            Assert.DoesNotContain("typeof(global::Bad)", result.Registration);
        }

        [Fact]
        public void AbstractRoot_IsRefused_Test()
        {
            var result = Run(Preamble + @"
[XmlInclude(typeof(Concrete))]
public abstract class Root
{
    public int Number { get; set; }
}

public class Concrete : Root
{
    public string Name { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Root));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("abstract root", Assert.Single(result.CompatRefusals).GetMessage());
        }

        [Fact]
        public void NestedCollection_IsRefused_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public List<List<int>> Matrix { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("nested collections", Assert.Single(result.CompatRefusals).GetMessage());
        }

        [Fact]
        public void NoParameterlessConstructor_IsRefused_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public Bad(int number) { Number = number; }

    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("parameterless constructor", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Отдельная порода отказов: не «генератор не умеет такой тип», а «генератор
        /// молча игнорирует такой атрибут». Она опаснее - неподдержанный тип роняет
        /// генерацию, а непонятый атрибут выдаёт валидный код и <b>другой</b> документ,
        /// причём тип отчитывается ускоренным.
        ///
        /// Хуже всех здесь пространство имён: документ без объявления штатный
        /// сериализатор не прочитает вовсе, то есть молчание стоило бы не косметики,
        /// а совместимости.
        /// </summary>
        [Theory]
        [InlineData("[XmlRoot(Namespace = \"urn:x\")] public class Bad { public int Number { get; set; } }", "namespaces")]
        [InlineData("[XmlType(Namespace = \"urn:x\")] public class Bad { public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlElement(Namespace = \"urn:x\")] public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlAttribute(Namespace = \"urn:x\")] public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlArray(Namespace = \"urn:x\")] public List<int> Values { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlAnyElement] public object Any { get; set; } }", "XmlAnyElement")]
        [InlineData("public class Bad { [XmlAnyAttribute] public System.Xml.XmlAttribute[] Any { get; set; } }", "XmlAnyAttribute")]
        [InlineData("public class Bad { [XmlChoiceIdentifier(\"Kind\")] public object Value { get; set; } public int Kind { get; set; } }", "XmlChoiceIdentifier")]
        [InlineData("public class Bad { [XmlNamespaceDeclarations] public XmlSerializerNamespaces Ns { get; set; } }", "XmlNamespaceDeclarations")]
        [InlineData("public class Bad { [XmlElement(\"a\", typeof(int))] public object Value { get; set; } }", "type-driven")]
        [InlineData("public class Bad { [XmlElement(\"a\")] [XmlElement(\"b\")] public object Value { get; set; } }", "type-driven")]
        //DataType задаёт лексическую форму значения: с ним BCL пишет DateTime как
        //"2020-01-02", а byte[] как "01FF" вместо base64 (снято прогоном)
        [InlineData("public class Bad { [XmlElement(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlAttribute(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlElement(DataType = \"hexBinary\")] public byte[] Data { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlArrayItem(DataType = \"date\")] public List<DateTime> Days { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlText(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        //[Flags] хуже всех остальных: документ выходит такой, который штатный
        //сериализатор прочитать не может вовсе - он пишет "Read Write", а
        //Enum.ToString() даёт "Read, Write" (проверено в обе стороны)
        [InlineData("[Flags] public enum Access { None = 0, Read = 1, Write = 2 } public class Bad { public Access Rights { get; set; } }", "[Flags]")]
        [InlineData("[Flags] public enum Access { None = 0, Read = 1, Write = 2 } public class Bad { public List<Access> Rights { get; set; } }", "[Flags]")]
        public void SilentlyIgnoredAttribute_RefusesWholeType_Test(string declaration, string because)
        {
            var result = Run(Preamble + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains(because, Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Форму документа такой тип решает сам, в своих <c>ReadXml</c>/<c>WriteXml</c>,
        /// а генератор обошёл бы его члены и написал совсем другое.
        /// </summary>
        [Fact]
        public void XmlSerializable_IsRefused_Test()
        {
            var result = Run(Preamble + @"
using System.Xml;
using System.Xml.Schema;

public class Bad : System.Xml.Serialization.IXmlSerializable
{
    public int Number { get; set; }

    public XmlSchema GetSchema() => null;
    public void ReadXml(XmlReader reader) { }
    public void WriteXml(XmlWriter writer) { }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("IXmlSerializable", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Отказ обязан срабатывать и вглубь графа, а не только на корне: атрибут
        /// на ребёнке ломает документ ровно так же.
        /// </summary>
        [Fact]
        public void UnsupportedAttributeOnChild_RefusesRoot_Test()
        {
            var result = Run(Preamble + @"
public class Root
{
    public Child Child { get; set; }
}

[XmlType(Namespace = ""urn:x"")]
public class Child
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Root));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("global::Child", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Обратная сторона: имя без пространства имён - это ровно то, ради чего
        /// разбор штатных атрибутов и делался, и отказывать здесь не за что.
        /// </summary>
        [Fact]
        public void PlainNames_StayAccelerated_Test()
        {
            var result = Run(Preamble + @"
[XmlRoot(""root"")]
[XmlType(""bad"")]
public class Ok
{
    [XmlElement(""n"")]
    public int Number { get; set; }

    [XmlAttribute(""id"")]
    public int Id { get; set; }

    [XmlArray(""values"")]
    [XmlArrayItem(""value"")]
    public List<int> Values { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        /// <summary>
        /// Обычное перечисление отказом не является: отказывает только <c>[Flags]</c>.
        /// Без этой проверки правка «отказаться от флагов» тихо унесла бы с собой
        /// все перечисления вообще.
        /// </summary>
        [Fact]
        public void PlainEnum_StaysAccelerated_Test()
        {
            var result = Run(Preamble + @"
public enum Kind { First = 1, Second = 2 }

public class Ok
{
    public Kind Kind { get; set; }
    public List<Kind> Kinds { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        /// <summary>
        /// Точка вызова - не только конструктор. <c>FromTypes</c> называет несколько
        /// типов сразу, и все они обязаны попасть в реестр: иначе подмена одной
        /// строкой теряет ускорение везде, где проект пользуется фабричным методом,
        /// причём молча.
        /// </summary>
        [Fact]
        public void FromTypesCallSite_RegistersEveryNamedType_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }
public class Second { public string Name { get; set; } }

public static class Entry
{
    public static object Make() => XmlSerializer.FromTypes(new[] { typeof(First), typeof(Second), });
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::First)", result.Registration);
            Assert.Contains("typeof(global::Second)", result.Registration);
        }

        [Fact]
        public void FromTypesCallSite_WithExplicitArrayType_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => XmlSerializer.FromTypes(new Type[] { typeof(First), });
}
");

            Assert.Contains("typeof(global::First)", result.Registration);
        }

        /// <summary>
        /// Массив, приехавший переменной, на этапе сборки не раскрывается - как и
        /// <c>typeof</c> в переменной. Это не отказ (отказывать не от чего: типа мы
        /// не знаем), а просто отсутствие точки вызова.
        /// </summary>
        [Fact]
        public void FromTypesCallSite_WithVariableArray_IsNotACallSite_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static readonly Type[] Types = new[] { typeof(First), };

    public static object Make() => XmlSerializer.FromTypes(Types);
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        [Fact]
        public void FactoryCallSite_IsRegistered_Test()
        {
            var result = Run(Preamble + @"
using XmlSerializerFactory = XmlSerDe.Compat.XmlSerializerFactory;

public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => new XmlSerializerFactory().CreateSerializer(typeof(First));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::First)", result.Registration);
        }

        /// <summary>
        /// Штатная фабрика - не наша: её вызов ускорять нечем, и точкой вызова он
        /// не является. Иначе мы регистрировали бы типы, которые фасад в глаза
        /// не увидит.
        /// </summary>
        [Fact]
        public void BclFactoryCallSite_IsIgnored_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => new XmlSerializerFactory().CreateSerializer(typeof(First));
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        /// <summary>
        /// Строгий режим не меняет решения - он меняет громкость: проект, который
        /// на ускорение рассчитывает, узнаёт об отказе ошибкой сборки, а не
        /// строчкой в подробном логе.
        /// </summary>
        [Theory]
        [InlineData(null, DiagnosticSeverity.Info)]
        [InlineData("false", DiagnosticSeverity.Info)]
        [InlineData("true", DiagnosticSeverity.Warning)]
        [InlineData("warning", DiagnosticSeverity.Warning)]
        [InlineData("error", DiagnosticSeverity.Error)]
        public void StrictMode_ChangesSeverityOnly_Test(string strict, DiagnosticSeverity expected)
        {
            var result = Run(Preamble + @"
public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
", strict);

            Assert.Null(result.Registration);
            Assert.Equal(expected, Assert.Single(result.CompatRefusals).Severity);
        }

        #region прогон генератора

        private sealed class GeneratorRun
        {
            public string Registration { get; set; }
            public List<Diagnostic> CompatRefusals { get; set; }
        }

        private static GeneratorRun Run(string source, string strict = null)
        {
            var compilation = CSharpCompilation.Create(
                "CompatGeneratorFixtureAssembly",
                new[] { CSharpSyntaxTree.ParseText(source), },
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), },
                optionsProvider: new GeneratorHarness.StrictOptionsProvider(strict)
                );

            var result = driver
                .RunGenerators(compilation)
                .GetRunResult();

            var registration = result.GeneratedTrees
                .FirstOrDefault(t => t.FilePath.EndsWith("XmlSerDe.Compat.Registration.g.cs", StringComparison.Ordinal));

            return new GeneratorRun
            {
                Registration = registration?.ToString(),
                CompatRefusals = result.Diagnostics
                    .Where(d => d.Id == "XMLSERDE001")
                    .ToList(),
            };
        }

        #endregion
    }
}
