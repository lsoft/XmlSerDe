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
