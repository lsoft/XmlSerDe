using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Сгенерированный текст по docs/opt-in-xml-guards.md §11.5: хост без
    /// <c>[XmlGuards]</c> не упоминает ни одного примитива стражей и не тащит
    /// лишнего параметра; хост с одним флагом содержит только его; compat
    /// получает полный набор без атрибута в пользовательском коде; в
    /// <c>.g.cs</c> нет ни <c>XmlGuard</c>, ни <c>HasFlag</c>.
    ///
    /// Проверка текста, а не поведения: страж, случайно включённый всем,
    /// поведением на well-formed документе не виден вовсе.
    /// </summary>
    public class XmlGuardGeneratorFixture
    {
        private const string Subjects = @"
namespace Sample
{
    public class Node
    {
        public int Number { get; set; }
        public string Name { get; set; }
    }
}
";

        /// <summary>
        /// Примитивы стражей: ни одного из них не должно быть у хоста без
        /// атрибута.
        /// </summary>
        private static readonly string[] GuardPrimitives =
        {
            "EnsureNoTrailingContent",
            "EnsureNoTrailingContentMarkup",
            "EnsureEndTagName",
            "EnsureUniqueAttributes",
            "EnsureValidInputChars",
            "ThrowUnexpectedEof",
            "expectedEndName",
            "XmlDocumentErrors",
            "XmlDocumentException",
        };

        [Fact]
        public void DefaultHost_OmitsEveryGuardPrimitive()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class DefaultHost
    {
    }
}
", "DefaultHost.g.cs");

            foreach (var primitive in GuardPrimitives)
            {
                Assert.DoesNotContain(primitive, host, StringComparison.Ordinal);
            }

            //корневой вход не забирает съеденное и не заворачивает исключение
            Assert.Contains("out result, out _);", host, StringComparison.Ordinal);
            Assert.DoesNotContain("try", host, StringComparison.Ordinal);
            Assert.DoesNotContain("catch", host, StringComparison.Ordinal);
        }

        [Fact]
        public void GuardNone_IsTextuallyIdenticalToNoAttribute()
        {
            var without = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class Host
    {
    }
}
", "Host.g.cs");

            var none = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.None)]
    [XmlSubject(typeof(Node), true)]
    public partial class Host
    {
    }
}
", "Host.g.cs");

            Assert.Equal(without, none);
        }

        [Fact]
        public void SingleRootHost_CallsTailCheckAndWraps()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlSubject(typeof(Node), true)]
    public partial class SingleRootHost
    {
    }
}
", "SingleRootHost.g.cs");

            Assert.Contains("XmlScan.EnsureNoTrailingContent(", host, StringComparison.Ordinal);
            Assert.Contains("out result, out var bodyConsumed);", host, StringComparison.Ordinal);
            Assert.Contains("XmlDocumentErrors.Wrap(exception)", host, StringComparison.Ordinal);

            //без Markup хвост не разбирает разметку
            Assert.DoesNotContain("EnsureNoTrailingContentMarkup", host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Лишний параметр появляется только у своего флага, и вместе с ним -
        /// обе вставки в цикл.
        /// </summary>
        [Fact]
        public void MatchingEndTagsHost_PassesExpectedNameAndChecksBothEnds()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.MatchingEndTags)]
    [XmlSubject(typeof(Node), true)]
    public partial class EndTagHost
    {
    }
}
", "EndTagHost.g.cs");

            Assert.Contains("roschar expectedEndName, out", host, StringComparison.Ordinal);
            Assert.Contains("EnsureEndTagName(child.DeclaredNodeType, expectedEndName);", host, StringComparison.Ordinal);
            Assert.Contains("if(!expectedEndName.IsEmpty)", host, StringComparison.Ordinal);
            Assert.Contains("ThrowUnexpectedEof();", host, StringComparison.Ordinal);

            //нода без тела закрывающего тега не имеет, и требовать его нельзя
            Assert.Contains("IsBodyless ? roschar.Empty :", host, StringComparison.Ordinal);

            //хвост документа этот флаг не трогает
            Assert.DoesNotContain("EnsureNoTrailingContent", host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Единственная точка встречи двух осей в этом флаге: тот же
        /// <c>[XmlGuards]</c> плюс <c>[XmlFeatures]</c> меняет имя проверки
        /// хвоста (§5.3).
        /// </summary>
        [Fact]
        public void SingleRootHost_WithMarkup_CallsMiscAwareTailCheck()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlFeatures(XmlFeature.Markup)]
    [XmlSubject(typeof(Node), true)]
    public partial class MarkupHost
    {
    }
}
", "MarkupHost.g.cs");

            Assert.Contains("EnsureNoTrailingContentMarkup(", host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Флаг, который хвоста не касается, хвоста и не касается: единственное
        /// отличие такого хоста от default - форма исключения.
        /// </summary>
        [Fact]
        public void ForeignRootNamespaceHost_ChecksRootHeadOnlyOnce()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.ForeignRootNamespace)]
    [XmlSubject(typeof(Node), true)]
    public partial class NsHost
    {
    }
}
", "NsHost.g.cs");

            //ровно один вызов: проверка живёт на корне. На каждом элементе она
            //стоила бы столько же, сколько сам разбор головы, а ловила бы то,
            //чего страж и не обещает
            var count = 0;
            var index = 0;
            while ((index = host.IndexOf("EnsureNoForeignRootNamespace", index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += "EnsureNoForeignRootNamespace".Length;
            }

            Assert.Equal(1, count);
        }

        [Fact]
        public void OtherGuardHost_DoesNotCheckRootNamespace()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlSubject(typeof(Node), true)]
    public partial class OtherHost
    {
    }
}
", "OtherHost.g.cs");

            Assert.DoesNotContain("EnsureNoForeignRootNamespace", host, StringComparison.Ordinal);
        }

        [Fact]
        public void OtherGuardHost_DoesNotCheckTail()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlSubject(typeof(Node), true)]
    public partial class UniqueHost
    {
    }
}
", "UniqueHost.g.cs");

            Assert.DoesNotContain("EnsureNoTrailingContent", host, StringComparison.Ordinal);
            Assert.Contains("out result, out _);", host, StringComparison.Ordinal);
            Assert.Contains("XmlDocumentErrors.Wrap(exception)", host, StringComparison.Ordinal);

            //свой примитив на месте, и он под проверкой HasAttributes: голова
            //без атрибутов за этот флаг не платит
            Assert.Contains(
                "if(xmlNode.HasAttributes) global::XmlSerDe.Internal.XmlScan.EnsureUniqueAttributes(",
                host,
                StringComparison.Ordinal
                );
        }

        /// <summary>
        /// Строку разбирает инжектор, и страж не заменяет его собой: проверка
        /// стоит после разбора, поэтому она одна на оба пути - и на инжектор,
        /// и на декодер CDATA.
        /// </summary>
        [Fact]
        public void IllegalCharsHost_ChecksStringsAfterParse()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.IllegalChars)]
    [XmlSubject(typeof(Node), true)]
    public partial class CharHost
    {
    }
}
", "CharHost.g.cs");

            Assert.Contains("inj.ParseBody(", host, StringComparison.Ordinal);
            Assert.Contains("XmlCharGuard.EnsureValidInputChars(", host, StringComparison.Ordinal);

            //разметку и числа страж не трогает
            Assert.DoesNotContain("EnsureValidInputChars(body", host, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureNoTrailingContent", host, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureUniqueAttributes", host, StringComparison.Ordinal);
        }

        [Fact]
        public void GeneratedText_MentionsNeitherEnumNorHasFlag()
        {
            var host = RunHost(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.SystemXmlCompatible)]
    [XmlSubject(typeof(Node), true)]
    public partial class FullHost
    {
    }
}
", "FullHost.g.cs");

            Assert.DoesNotContain("XmlGuard.", host, StringComparison.Ordinal);
            Assert.DoesNotContain("HasFlag", host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Фасад включает полный набор сам, а native-хост в той же компиляции
        /// от этого не меняется ни на строчку.
        /// </summary>
        [Fact]
        public void Compat_GetsFullSet_NativeHostStaysUntouched()
        {
            const string HostSource = @"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class NativeHost
    {
    }
}
";

            const string WithCompat = HostSource + @"
namespace Sample
{
    public static class Entry
    {
        public static object Make() => new XmlSerDe.Compat.XmlSerializer(typeof(Node));
    }
}
";

            var alone = File(Run(HostSource), "NativeHost.g.cs");
            var withCompat = Run(WithCompat);

            Assert.Equal(alone, File(withCompat, "NativeHost.g.cs"));

            //файлов у фасада несколько (сериализатор и регистрация), нужен тот,
            //где живёт разбор
            var compat = File(withCompat, "XmlSerDe.Compat.Serializer.g.cs");

            Assert.Contains("EnsureNoTrailingContentMarkup(", compat, StringComparison.Ordinal);
            Assert.Contains("XmlDocumentErrors.Wrap(exception)", compat, StringComparison.Ordinal);
            Assert.DoesNotContain("XmlGuards", WithCompat, StringComparison.Ordinal);
        }

        private static string RunHost(string hostSource, string hintName)
        {
            return File(Run(hostSource), hintName);
        }

        private static string File(GeneratorDriverRunResult result, string hintName)
        {
            var source = result.Results.Single().GeneratedSources
                .SingleOrDefault(s => GeneratorHarness.IsHint(s.HintName, hintName));

            Assert.True(source.HintName != null, "missing generated file " + hintName);
            return source.SourceText.ToString();
        }

        private static GeneratorDriverRunResult Run(string hostSource)
        {
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(Subjects, path: "subject.cs"),
                CSharpSyntaxTree.ParseText(hostSource, path: "host.cs"),
            };

            var compilation = CSharpCompilation.Create(
                "XmlGuardGeneratorFixtureAssembly",
                trees,
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), }
                );

            return driver.RunGenerators(compilation).GetRunResult();
        }
    }
}
