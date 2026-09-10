using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Формы POCO, на которых генератор либо выдавал некомпилируемый код, либо
    /// молча терял хост, либо падал целиком. Здесь генератор гоняется через
    /// <c>CSharpGeneratorDriver</c>, а его вывод компилируется вместе с исходником:
    /// ошибка сборки пользователя видна как диагностика компиляции, а не как
    /// красный проект тестов.
    /// </summary>
    public class GeneratedShapeCompileFixture
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
";

        /// <summary>
        /// Абстрактная база с <c>[XmlAttribute]</c>: метод разбора атрибутов для неё
        /// не генерируется, а вызов - генерировался, и аргумент базового типа не
        /// подходил к перегрузке наследника (CS1503).
        /// </summary>
        [Fact]
        public void AbstractBaseWithAttributeMember_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    [XmlInclude(typeof(Derived))]
    public abstract class Base { [XmlAttribute] public int Id { get; set; } }
    public class Derived : Base { public int Value { get; set; } }
    public class Holder { public Base Item { get; set; } }

    [XmlSubject(typeof(Holder), true)]
    [XmlSubject(typeof(Base), false)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// <c>[DefaultValue(E.Unknown)]</c> с отрицательной константой: литерал
        /// <c>(global::E)-1</c> C# читает как вычитание (CS0075).
        /// </summary>
        [Fact]
        public void NegativeEnumDefaultValue_Compiles()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public enum Kind { Unknown = -1, One = 1 }
    public class Holder { [DefaultValue(Kind.Unknown)] public Kind Kind { get; set; } }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.Contains("(-1)", run.Host, StringComparison.Ordinal);
        }

        /// <summary>
        /// System.Xml.Serialization пропускает readonly-поле (кроме коллекции, которую
        /// наполняет через Add) и пишет init-свойство. Присвоить init-свойство из
        /// сгенерированного кода нельзя, поэтому оно пропускается так же, как
        /// свойство без сеттера; главное - сборка не ломается (CS0191, CS8852).
        /// </summary>
        [Fact]
        public void ReadonlyFieldAndInitOnlyProperty_AreSkipped()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class Holder
    {
        public readonly int X = 5;
        public int Y { get; init; }
        public readonly List<int> L = new List<int>();
        public int Z { get; set; }
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.DoesNotContain("result.X =", run.Host, StringComparison.Ordinal);
            Assert.DoesNotContain("result.Y =", run.Host, StringComparison.Ordinal);
            Assert.Contains("obj.L", run.Host, StringComparison.Ordinal);
            Assert.Contains("result.Z =", run.Host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Член, скрытый через <c>new</c>: обход по слоям наследования отдавал оба,
        /// и локальная переменная объявлялась дважды (CS0128). System.Xml.Serialization
        /// берёт член наследника и оставляет его на месте базового.
        /// </summary>
        [Fact]
        public void HiddenMember_IsTakenOnceFromTheMostDerivedType()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class Base { public string Name { get; set; } public int A { get; set; } }
    public class Derived : Base { public new string Name { get; set; } public int C { get; set; } }

    [XmlSubject(typeof(Derived), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.Equal(1, CountOf(run.Host, "var NameSpan ="));

            //порядок как у BCL: Name на месте базового, потом A, потом C
            var name = run.Host.IndexOf("var NameSpan =", StringComparison.Ordinal);
            var a = run.Host.IndexOf("var ASpan =", StringComparison.Ordinal);
            var c = run.Host.IndexOf("var CSpan =", StringComparison.Ordinal);
            Assert.True(name < a && a < c, "expected member order Name, A, C");
        }

        /// <summary>
        /// Имя члена - ключевое слово C#: без <c>@</c> обращение <c>obj.class</c>
        /// не разбирается.
        /// </summary>
        [Fact]
        public void KeywordMemberName_IsEscaped()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class Holder
    {
        public string @class { get; set; }
        [XmlAttribute] public int @int { get; set; }
        public List<int> @event { get; set; }
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
        }

        /// <summary>
        /// Имя сгенерированного файла строилось из простого имени класса, и второй
        /// одноимённый хост из другого пространства имён молча оставался без
        /// сгенерированной половины.
        /// </summary>
        [Fact]
        public void TwoHostsWithSameSimpleName_AreBothGenerated()
        {
            var run = Compile(Preamble + @"
namespace A
{
    public class Item { public int X { get; set; } }
    [XmlSubject(typeof(Item), true)]
    public partial class Serializer { }
}
namespace B
{
    public class Item { public int Y { get; set; } }
    [XmlSubject(typeof(Item), true)]
    public partial class Serializer
    {
        public static string Touch() { var sb = new StringBuilderExhauster(); Serialize(sb, new Item(), false); return sb.ToString(); }
    }
}
");

            AssertCompiles(run);
            Assert.Equal(2, run.Sources.Count(s => s.Key.EndsWith("Serializer.g.cs", StringComparison.Ordinal)));
        }

        /// <summary>
        /// Свойство без сеттера с типом на два типовых аргумента роняло генератор
        /// целиком общей ошибкой со стеком. System.Xml.Serialization такой член
        /// без сеттера просто не видит.
        /// </summary>
        [Fact]
        public void GetterOnlyDictionary_IsSkippedWithoutCrash()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class Holder
    {
        public int A { get; set; }
        public Dictionary<string, int> Map { get; } = new Dictionary<string, int>();
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.DoesNotContain("Map", run.Host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Два наследника с одним простым именем из разных пространств имён
        /// получали один и тот же <c>xsi:type</c> и молча читались как первый.
        /// System.Xml.Serialization отказывает при построении сериализатора;
        /// здесь отказ - ошибка генератора с обоими именами.
        /// </summary>
        [Fact]
        public void SameXmlTypeNameInDifferentNamespaces_IsReported()
        {
            var run = Compile(Preamble + @"
namespace NA { public class Item : Sample.Base { public int X { get; set; } } }
namespace NB { public class Item : Sample.Base { public int Y { get; set; } } }
namespace Sample
{
    [XmlInclude(typeof(NA.Item))]
    [XmlInclude(typeof(NB.Item))]
    public class Base { }
    public class Holder { public Base Item { get; set; } }

    [XmlSubject(typeof(Holder), true)]
    [XmlSubject(typeof(Base), false)]
    public partial class Host { }
}
");

            var error = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var message = error.GetMessage();
            Assert.Contains("NA.Item", message, StringComparison.Ordinal);
            Assert.Contains("NB.Item", message, StringComparison.Ordinal);
        }

        #region прогон генератора и компиляция его вывода

        private sealed class CompileRun
        {
            public Dictionary<string, string> Sources { get; set; } = new();
            public List<Diagnostic> GeneratorDiagnostics { get; set; } = new();
            public List<Diagnostic> CompileErrors { get; set; } = new();

            public string Host => Sources.Single(s => s.Key.EndsWith("Host.g.cs", StringComparison.Ordinal)).Value;
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

        private static int CountOf(string text, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }

        private static CompileRun Compile(string source)
        {
            var compilation = CSharpCompilation.Create(
                "GeneratedShapeCompileFixtureAssembly",
                new[] { CSharpSyntaxTree.ParseText(source, path: "source.cs"), },
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), }
                );

            driver
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics)
                .GetRunResult();

            var run = new CompileRun
            {
                GeneratorDiagnostics = generatorDiagnostics.ToList(),
                CompileErrors = output.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList(),
            };

            foreach (var tree in output.SyntaxTrees)
            {
                if (tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
                {
                    run.Sources[tree.FilePath] = tree.ToString();
                }
            }

            return run;
        }

        #endregion
    }
}
