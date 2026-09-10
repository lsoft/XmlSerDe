using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Generated text from docs/opt-in-xml-features.md §10.3: default hosts
    /// must not mention opt-in primitives or document-wide heuristics; a CData
    /// host must call the CDATA primitive; two hosts in one compilation stay
    /// distinct; compat injects the full set without a user [XmlFeatures].
    /// </summary>
    public class XmlFeatureGeneratorFixture
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

        private static readonly string[] DefaultForbidden =
        {
            "IsXmlCommentExistsHeuristic",
            "IsCDataBlockExistsHeuristic",
            "CDataHead",
            "EnsureValidXmlChars",
            "DecodeElementTextWithCData",
            "ReadHeadMarkup",
            "ReadTextBodyMarkup",
            "GetPreciseNodeType",
            "SkipBodyMarkup",
        };

        [Fact]
        public void DefaultHost_OmitsOptInPrimitivesAndHeuristics()
        {
            var host = RunHost(@"
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class DefaultHost
    {
    }
}
", "DefaultHost.g.cs");

            foreach (var forbidden in DefaultForbidden)
            {
                Assert.DoesNotContain(forbidden, host, StringComparison.Ordinal);
            }

            Assert.Contains("XmlScan.ReadHead(", host, StringComparison.Ordinal);

            //строка без CData по-прежнему идёт через инжектор
            Assert.Contains("inj.ParseBody(", host, StringComparison.Ordinal);
            Assert.DoesNotContain("XmlTextDecoder.", host, StringComparison.Ordinal);

            //запись строки - через exhauster и без guard'а; Append(Encode(...))
            //здесь быть не должно: он аллоцирует и роняет null
            Assert.Contains("AppendEncodedUnchecked(", host, StringComparison.Ordinal);
            Assert.DoesNotContain("AppendEncoded(", host, StringComparison.Ordinal);
            Assert.DoesNotContain("XmlTextEncoder.Encode", host, StringComparison.Ordinal);

            //пролог снимается по флагам этого хоста, а не по общему умолчанию
            Assert.Contains("CutXmlHead(xml)", host, StringComparison.Ordinal);
        }

        [Fact]
        public void CDataHost_CallsCDataPrimitive_WithoutDocumentHeuristic()
        {
            var host = RunHost(@"
using XmlSerDe.Common;

namespace Sample
{
    [XmlFeatures(XmlFeature.CData)]
    [XmlSubject(typeof(Node), true)]
    public partial class CDataHost
    {
    }
}
", "CDataHost.g.cs");

            Assert.Contains("DecodeElementTextWithCData", host, StringComparison.Ordinal);
            Assert.Contains("ReadHeadMarkup", host, StringComparison.Ordinal);
            Assert.DoesNotContain("IsCDataBlockExistsHeuristic", host, StringComparison.Ordinal);
            Assert.DoesNotContain("IsXmlCommentExistsHeuristic", host, StringComparison.Ordinal);

            //CData - единственная фича, которая снимает строку с инжектора:
            //у IInjector нет перегрузки, понимающей CDATA
            Assert.Contains("XmlTextDecoder.DecodeElementTextWithCData(", host, StringComparison.Ordinal);
        }

        [Fact]
        public void MarkupHost_CutXmlHead_CarriesItsOwnFlag()
        {
            var host = RunHost(@"
using XmlSerDe.Common;

namespace Sample
{
    [XmlFeatures(XmlFeature.Markup)]
    [XmlSubject(typeof(Node), true)]
    public partial class MarkupHost
    {
    }
}
", "MarkupHost.g.cs");

            Assert.Contains("CutXmlHead(true, xml)", host, StringComparison.Ordinal);

            //Markup без CData инжектор не обходит
            Assert.Contains("inj.ParseBody(", host, StringComparison.Ordinal);
            Assert.DoesNotContain("DecodeElementTextWithCData", host, StringComparison.Ordinal);
        }

        [Fact]
        public void CharGuardHost_CallsAppendEncoded_DefaultDoesNot()
        {
            var guarded = RunHost(@"
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlFeatures(XmlFeature.CharGuard)]
    [XmlSubject(typeof(Node), true)]
    public partial class GuardHost
    {
    }
}
", "GuardHost.g.cs");

            Assert.Contains("AppendEncoded(", guarded, StringComparison.Ordinal);
            Assert.DoesNotContain("XmlTextEncoder.Encode", guarded, StringComparison.Ordinal);
        }

        [Fact]
        public void TwoHosts_InOneCompilation_EmitDifferentText()
        {
            var result = Run(@"
using XmlSerDe.Common;

namespace Sample
{
    [XmlSubject(typeof(Node), true)]
    public partial class DefaultHost
    {
    }

    [XmlFeatures(XmlFeature.CData)]
    [XmlSubject(typeof(Node), true)]
    public partial class CDataHost
    {
    }
}
");

            var defaultHost = File(result, "DefaultHost.g.cs");
            var cdataHost = File(result, "CDataHost.g.cs");

            Assert.NotEqual(defaultHost, cdataHost);
            Assert.DoesNotContain("DecodeElementTextWithCData", defaultHost, StringComparison.Ordinal);
            Assert.Contains("DecodeElementTextWithCData", cdataHost, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadHeadMarkup", defaultHost, StringComparison.Ordinal);
            Assert.Contains("ReadHeadMarkup", cdataHost, StringComparison.Ordinal);
        }

        [Fact]
        public void CompatOnly_EmitsOptInPrimitives_WithoutUserXmlFeatures()
        {
            const string source = @"
using System.Xml.Serialization;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace Sample
{
    public class Node
    {
        [XmlAttribute]
        public int Number { get; set; }
        public string Name { get; set; }
    }

    public static class Entry
    {
        public static object Make() => new XmlSerializer(typeof(Node));
    }
}
";

            Assert.DoesNotContain("XmlFeatures", source, StringComparison.Ordinal);

            var result = Run(source, includeSubjects: false);
            var compat = File(result, "XmlSerDe.Compat.Serializer.g.cs");

            Assert.Contains("ReadHeadMarkup", compat, StringComparison.Ordinal);
            Assert.Contains("DecodeElementTextWithCData", compat, StringComparison.Ordinal);
            Assert.Contains("ParseAttribute(", compat, StringComparison.Ordinal);
            Assert.DoesNotContain("[XmlFeatures", compat, StringComparison.Ordinal);
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

        private static GeneratorDriverRunResult Run(string hostSource, bool includeSubjects = true)
        {
            var trees = includeSubjects
                ? new[]
                {
                    CSharpSyntaxTree.ParseText(Subjects, path: "subject.cs"),
                    CSharpSyntaxTree.ParseText(hostSource, path: "host.cs"),
                }
                : new[]
                {
                    CSharpSyntaxTree.ParseText(hostSource, path: "host.cs"),
                };

            var compilation = CSharpCompilation.Create(
                "XmlFeatureGeneratorFixtureAssembly",
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
