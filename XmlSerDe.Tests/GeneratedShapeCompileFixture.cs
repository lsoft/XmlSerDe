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
using XmlSerDe;
using XmlSerDe.Internal;

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
        /// Непубличный член не попадает в обмен вовсе - ни на запись, ни на чтение.
        /// Это правило <c>System.Xml.Serialization</c>, который берёт члены через
        /// <c>BindingFlags.Public</c> (снято прогоном: <c>internal</c> ему так же
        /// невидим, как <c>private</c>).
        ///
        /// Раньше отбор выбрасывал только <c>private</c> и <c>protected</c>, и
        /// <c>internal</c>-член уезжал в документ, которого у штатного сериализатора
        /// на том же типе нет. Ломающее изменение: из документов, где такой элемент
        /// был, он пропадёт.
        /// </summary>
        [Fact]
        public void NonPublicMembers_AreSkipped()
        {
            var run = Compile(Preamble + @"
namespace Sample
{
    public class Holder
    {
        public int Taken { get; set; }
        internal int HiddenInternalProperty { get; set; }
        internal int HiddenInternalField;
        protected internal int HiddenProtectedInternal { get; set; }
        private protected int HiddenPrivateProtected { get; set; }
        protected int HiddenProtected { get; set; }
        private int HiddenPrivate { get; set; }
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.Contains("result.Taken =", run.Host, StringComparison.Ordinal);

            //одна общая приставка, потому что искать по слову «Internal» нельзя:
            //сгенерированный код сам живёт рядом с пространством XmlSerDe.Internal
            Assert.DoesNotContain("Hidden", run.Host, StringComparison.Ordinal);
        }

        /// <summary>
        /// <c>required</c> роняло сборку <b>потребителя</b>: генератор писал
        /// <c>new T()</c>, а компилятор требовал задать член в инициализаторе
        /// объекта - CS9035 в файле, которого потребитель не писал. Фолбэка
        /// на родном пути нет, поэтому исход - диагностика генератора, называющая
        /// тип и член, и без стектрейса: причина названа словами, читать стек
        /// здесь некому.
        /// </summary>
        [Fact]
        public void RequiredMember_IsReportedInsteadOfBrokenCode()
        {
            var run = Compile(Preamble + RequiredPolyfills + @"
namespace Sample
{
    public class Holder
    {
        public int Z { get; set; }
        public required int Y { get; set; }
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            var error = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var message = error.GetMessage();
            Assert.Contains("Sample.Holder", message, StringComparison.Ordinal);
            Assert.Contains("Y", message, StringComparison.Ordinal);
            Assert.Contains("required", message, StringComparison.Ordinal);
            Assert.DoesNotContain("XmlSerDe.Generator.Producer", message, StringComparison.Ordinal);

            Assert.DoesNotContain(run.CompileErrors, d => d.Id == "CS9035");
        }

        /// <summary>
        /// Конструктор без параметров, помеченный <c>[SetsRequiredMembers]</c>,
        /// делает <c>new T()</c> законным - и отказывать не за что.
        /// </summary>
        [Fact]
        public void RequiredMemberWithSetsRequiredMembers_Compiles()
        {
            var run = Compile(Preamble + RequiredPolyfills + @"
namespace Sample
{
    public class Holder
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public Holder() { }

        public int Z { get; set; }
        public required int Y { get; set; }
    }

    [XmlSubject(typeof(Holder), true)]
    public partial class Host { }
}
");

            AssertCompiles(run);
            Assert.Contains("result.Y =", run.Host, StringComparison.Ordinal);
        }

        /// <summary>
        /// Под net472 этих типов нет; компилятор ищет их по полному имени, а не по
        /// сборке, поэтому объявления в самом исходнике достаточно. Под net8/net10
        /// они уже есть в corlib, и объявлять их второй раз нельзя - ссылки
        /// тестовая сборка берёт у себя самой, и оба объявления оказались бы видимы
        /// одновременно.
        /// </summary>
        private static string RequiredPolyfills =>
            typeof(object).Assembly.GetType("System.Runtime.CompilerServices.RequiredMemberAttribute") is null
                ? @"
namespace System.Runtime.CompilerServices
{
    internal sealed class RequiredMemberAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) { }
    }
}
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
"
                : "";

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
        /// целиком общей ошибкой со стеком. Штатный сериализатор такой член не
        /// пропускает, а отказывается от типа целиком: при построении
        /// <c>new XmlSerializer(typeof(T))</c> он бросает
        /// <c>NotSupportedException</c> - "because it implements IDictionary"
        /// (снято прогоном). Здесь проверяется только то, что генератор не падает;
        /// расхождение с BCL закрыто на стороне фасада, где такой тип получает отказ
        /// и уходит в фолбэк за тем же самым исключением.
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
