using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XmlSerDe.Components.Injector;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Разбор атрибутов одним проходом: тип с двумя и более
    /// <c>[XmlAttribute]</c>-членами разбирает голову один раз и раздаёт
    /// атрибуты по имени, вместо отдельного поиска на каждый член
    /// (README, «Cost of the <c>xsi:type</c> lookup»).
    ///
    /// Round-trip тесты этого перехода почти не замечают: документ, где
    /// атрибуты стоят в порядке объявления членов и ровно по одному, обе формы
    /// разбирают одинаково. Здесь собрано то, что при слиянии теряется молча:
    /// какой из дублей побеждает, считается ли атрибут с префиксом, переживает
    /// ли разбор чужие атрибуты между своими и обратный порядок.
    /// </summary>
    public class AttributeLoopFixture
    {
        #region поведение петли

        [Fact]
        public void DuplicateAttribute_FirstOneWins_Test()
        {
            //id="1" id="2" - не well-formed XML, его ловит XmlGuard.UniqueAttributes.
            //Хост без стража обязан вести себя как раньше: адресный поиск
            //останавливался на первом совпадении, значит и петля обязана
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" id=\"2\" tag=\"a\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void DuplicateStringAttribute_FirstOneWins_Test()
        {
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" tag=\"a\" tag=\"b\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("a", result.Tag);
        }

        [Fact]
        public void PrefixedAttribute_MatchesByLocalName_Test()
        {
            //поиск члена и раньше шёл с пустым требуемым префиксом, то есть
            //подходил любой; петля сравнивает только локальное имя - так же
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject ns:id=\"7\" tag=\"a\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(7, result.Id);
        }

        [Fact]
        public void AttributesInReverseOrder_AreBothAssigned_Test()
        {
            //петля идёт по документу, а не по объявлению членов
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject tag=\"a\" id=\"7\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(7, result.Id);
            Assert.Equal("a", result.Tag);
        }

        [Fact]
        public void ForeignAttributesBetweenKnownOnes_AreSkipped_Test()
        {
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject x=\"1\" id=\"7\" y=\"2\" tag=\"a\" z=\"3\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(7, result.Id);
            Assert.Equal("a", result.Tag);
        }

        [Fact]
        public void MissingAttribute_LeavesMemberAlone_Test()
        {
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"7\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(7, result.Id);
            Assert.Null(result.Tag);
        }

        [Fact]
        public void EntityAndWhitespaceInAttributeValue_AreStillNormalized_Test()
        {
            //нормализация §3.3.3 раньше делалась внутри ParseAttribute; теперь
            //перечисление отдаёт значение сырым, и декодирование зовётся
            //на месте присвоения - результат обязан остаться тем же
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" tag=\"a&amp;b\tc\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("a&b c", result.Tag);
        }

        #endregion

        #region выбор формы

        private const string Subjects = @"
using System.Xml.Serialization;

namespace Sample
{
    public class OneAttribute
    {
        [XmlAttribute(""a"")]
        public string A { get; set; }

        public int Number { get; set; }
    }

    public class TwoAttributes
    {
        [XmlAttribute(""a"")]
        public string A { get; set; }

        [XmlAttribute(""b"")]
        public string B { get; set; }

        public int Number { get; set; }
    }
}
";

        /// <summary>
        /// Слияние окупается не всегда: адресный поиск останавливается на
        /// найденном атрибуте, а перечисление всегда доходит до конца головы.
        /// На одном члене это проигрыш, поэтому там остаётся прежняя форма -
        /// и это проверяется текстом, потому что поведением две формы
        /// не отличаются вовсе.
        /// </summary>
        [Fact]
        public void SingleAttributeMember_KeepsTheAddressedLookup()
        {
            var text = RunHost("OneAttribute", "OneHost");

            Assert.Contains("XmlScan.ParseAttribute(", text);
            Assert.DoesNotContain("XmlScan.NextAttribute(", text);
        }

        [Fact]
        public void TwoAttributeMembers_UseTheSinglePassLoop()
        {
            var text = RunHost("TwoAttributes", "TwoHost");

            Assert.Contains("XmlScan.NextAttribute(", text);

            //ни одного адресного поиска атрибута в этом хосте остаться
            //не должно: оба члена раздаются из петли
            Assert.DoesNotContain("XmlScan.ParseAttribute(xmlNode.FullHead", text);
        }

        private static string RunHost(string subjectName, string hostName)
        {
            var hostSource = @"
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(" + subjectName + @"), true)]
    public partial class " + hostName + @"
    {
    }
}
";

            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(Subjects, path: "subject.cs"),
                CSharpSyntaxTree.ParseText(hostSource, path: "host.cs"),
            };

            var compilation = CSharpCompilation.Create(
                "AttributeLoopFixtureAssembly",
                trees,
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), }
                );

            var result = driver.RunGenerators(compilation).GetRunResult();

            var source = result.Results.Single().GeneratedSources
                .SingleOrDefault(s => GeneratorHarness.IsHint(s.HintName, hostName + ".g.cs"));

            Assert.True(source.HintName != null, "missing generated file " + hostName);
            return source.SourceText.ToString();
        }

        #endregion
    }
}
