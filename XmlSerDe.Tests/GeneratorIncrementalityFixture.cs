using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Инкрементальность генератора - свойство проверяемое, и проверяется оно здесь.
    ///
    /// Мерка одна: после правки, которая не меняет сгенерированный код, выходной шаг
    /// обязан быть <see cref="IncrementalStepRunReason.Cached"/>. Тогда Roslyn
    /// переиспользует уже разобранные деревья, и правка не тянет за собой повторный
    /// разбор и связывание всего сгенерированного - а это и есть основная цена
    /// генератора в IDE, а не его собственная работа.
    ///
    /// Обратная половина не менее важна и живёт в
    /// <see cref="MemberRename_InSubjectFile_RegeneratesOutput_Test"/>: <b>кэш обязан
    /// промахиваться, когда должен</b>. Порождается код по графу типов, а типы лежат
    /// в чужих файлах; генератор, заведённый строго от узла с атрибутом, был бы
    /// идеально инкрементален и выдавал бы устаревший код. Зелёный тест на кэш без
    /// этой пары ничего не стоит.
    /// </summary>
    public class GeneratorIncrementalityFixture
    {
        private const string HostFileName = "host.cs";
        private const string SubjectFileName = "subject.cs";

        private const string Host = @"
using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class Host
    {
    }

    public static class Entry
    {
        public static object Make() => new XmlSerializer(typeof(Node));
    }

    public static class Unrelated
    {
        public static int Value() => 1;
    }
}
";

        private const string Subject = @"
namespace Sample
{
    public class Node
    {
        public int Number { get; set; }
        public string Name { get; set; }
    }
}
";

        private const string HostWithCData = @"
using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlFeatures(XmlFeature.CData)]
    [XmlSubject(typeof(Node), true)]
    public partial class Host
    {
    }

    public static class Entry
    {
        public static object Make() => new XmlSerializer(typeof(Node));
    }

    public static class Unrelated
    {
        public static int Value() => 1;
    }
}
";

        private const string HostWithGuards = @"
using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlSubject(typeof(Node), true)]
    public partial class Host
    {
    }

    public static class Entry
    {
        public static object Make() => new XmlSerializer(typeof(Node));
    }

    public static class Unrelated
    {
        public static int Value() => 1;
    }
}
";

        /// <summary>
        /// Комментарий в файле с хостом: разобрано заново будет дерево, но состав
        /// генерации от этого не меняется ни на строчку.
        /// </summary>
        [Fact]
        public void Comment_InHostFile_KeepsOutputCached_Test()
        {
            var run = Run(editedHost: Host + Environment.NewLine + "//комментарий");

            AssertOutputCached(run);
        }

        [Fact]
        public void Comment_InSubjectFile_KeepsOutputCached_Test()
        {
            var run = Run(editedSubject: Subject + Environment.NewLine + "//комментарий");

            AssertOutputCached(run);
        }

        /// <summary>
        /// Правка тела метода - самое частое, что вообще делают в редакторе.
        /// К сериализуемому составу она отношения не имеет.
        /// </summary>
        [Fact]
        public void MethodBodyEdit_KeepsOutputCached_Test()
        {
            var run = Run(editedHost: Host.Replace("Value() => 1;", "Value() => 2;"));

            AssertOutputCached(run);
        }

        /// <summary>
        /// А вот переименование свойства в <b>другом файле</b> обязано дойти
        /// до сгенерированного кода. Здесь проверяется не скорость, а корректность:
        /// цена инкрементальности не должна платиться устаревшим кодом.
        /// </summary>
        [Fact]
        public void MemberRename_InSubjectFile_RegeneratesOutput_Test()
        {
            var run = Run(editedSubject: Subject.Replace("string Name", "string Title"));

            Assert.NotEqual(run.FirstSources, run.SecondSources);
            Assert.Contains("Name", run.FirstSources, StringComparison.Ordinal);
            Assert.Contains("Title", run.SecondSources, StringComparison.Ordinal);

            Assert.Contains(
                OutputReasons(run.Second),
                reason => reason != IncrementalStepRunReason.Cached
                );
        }

        [Fact]
        public void XmlFeatures_Added_RegeneratesOutput_Test()
        {
            var run = Run(editedHost: HostWithCData);

            Assert.NotEqual(run.FirstHost, run.SecondHost);
            Assert.Contains("DecodeElementTextWithCData", run.SecondHost, StringComparison.Ordinal);
            Assert.DoesNotContain("DecodeElementTextWithCData", run.FirstHost, StringComparison.Ordinal);

            Assert.Contains(
                OutputReasons(run.Second),
                reason => reason != IncrementalStepRunReason.Cached
                );
        }

        [Fact]
        public void XmlGuards_Added_RegeneratesOutput_Test()
        {
            var run = Run(editedHost: HostWithGuards);

            Assert.NotEqual(run.FirstHost, run.SecondHost);
            Assert.Contains("EnsureNoTrailingContent", run.SecondHost, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureNoTrailingContent", run.FirstHost, StringComparison.Ordinal);

            Assert.Contains(
                OutputReasons(run.Second),
                reason => reason != IncrementalStepRunReason.Cached
                );
        }

        [Fact]
        public void XmlGuards_Removed_RegeneratesOutput_Test()
        {
            var run = Run(initialHost: HostWithGuards, editedHost: Host);

            Assert.NotEqual(run.FirstHost, run.SecondHost);
            Assert.Contains("EnsureNoTrailingContent", run.FirstHost, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureNoTrailingContent", run.SecondHost, StringComparison.Ordinal);

            Assert.Contains(
                OutputReasons(run.Second),
                reason => reason != IncrementalStepRunReason.Cached
                );
        }

        [Fact]
        public void XmlFeatures_Removed_RegeneratesOutput_Test()
        {
            var run = Run(initialHost: HostWithCData, editedHost: Host);

            Assert.NotEqual(run.FirstHost, run.SecondHost);
            Assert.Contains("DecodeElementTextWithCData", run.FirstHost, StringComparison.Ordinal);
            Assert.DoesNotContain("DecodeElementTextWithCData", run.SecondHost, StringComparison.Ordinal);

            Assert.Contains(
                OutputReasons(run.Second),
                reason => reason != IncrementalStepRunReason.Cached
                );
        }

        /// <summary>
        /// Ни одного символа, дерева или компиляции в кэше конвейера: удержав их,
        /// генератор держал бы живой всю прошлую компиляцию целиком. Именно на это
        /// жалуются в ишью, и проверить это можно только так - обходом того, что
        /// драйвер реально сохранил между прогонами.
        /// </summary>
        [Fact]
        public void Pipeline_HoldsNoRoslynState_Test()
        {
            var run = Run(editedSubject: Subject + Environment.NewLine + "//комментарий");

            var steps = run.Second.Results.Single().TrackedSteps;
            var visited = new HashSet<object>(ReferenceComparer.Instance);

            foreach (var name in new[] { "Hosts", "CallSites", "Strict", "Generate", })
            {
                Assert.True(steps.ContainsKey(name), $"шаг '{name}' не отслеживается - тест проверяет пустоту");

                foreach (var step in steps[name])
                {
                    foreach (var output in step.Outputs)
                    {
                        AssertNoRoslynState(output.Value, visited, name);
                    }
                }
            }
        }

        #region прогон генератора

        private static void AssertOutputCached(IncrementalRun run)
        {
            Assert.Equal(run.FirstSources, run.SecondSources);

            Assert.All(
                OutputReasons(run.Second),
                reason => Assert.Equal(IncrementalStepRunReason.Cached, reason)
                );
        }

        private static IReadOnlyList<IncrementalStepRunReason> OutputReasons(GeneratorDriverRunResult result)
        {
            var reasons = result.Results.Single()
                .TrackedOutputSteps
                .SelectMany(pair => pair.Value)
                .SelectMany(step => step.Outputs)
                .Select(output => output.Reason)
                .ToList();

            Assert.NotEmpty(reasons);

            return reasons;
        }

        private sealed class IncrementalRun
        {
            public GeneratorDriverRunResult Second { get; set; } = null!;
            public string FirstSources { get; set; } = "";
            public string SecondSources { get; set; } = "";
            public string FirstHost { get; set; } = "";
            public string SecondHost { get; set; } = "";
        }

        /// <summary>
        /// Два прогона одним и тем же драйвером - иначе кэша между ними просто нет.
        /// Заменяются только те деревья, которые правка действительно затронула:
        /// повторный разбор неизменного файла сам по себе сбросил бы кэш и тест
        /// проверял бы не то, что написано в его названии.
        /// </summary>
        private static IncrementalRun Run(
            string? editedHost = null,
            string? editedSubject = null,
            string? initialHost = null
            )
        {
            var hostTree = CSharpSyntaxTree.ParseText(initialHost ?? Host, path: HostFileName);
            var subjectTree = CSharpSyntaxTree.ParseText(Subject, path: SubjectFileName);

            var compilation = CSharpCompilation.Create(
                "GeneratorIncrementalityFixtureAssembly",
                new[] { hostTree, subjectTree, },
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), },
                driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
                );

            driver = driver.RunGenerators(compilation);
            var first = driver.GetRunResult();

            if (editedHost is not null)
            {
                compilation = compilation.ReplaceSyntaxTree(
                    hostTree,
                    CSharpSyntaxTree.ParseText(editedHost, path: HostFileName)
                    );
            }
            if (editedSubject is not null)
            {
                compilation = compilation.ReplaceSyntaxTree(
                    subjectTree,
                    CSharpSyntaxTree.ParseText(editedSubject, path: SubjectFileName)
                    );
            }

            driver = driver.RunGenerators(compilation);
            var second = driver.GetRunResult();

            return new IncrementalRun
            {
                Second = second,
                FirstSources = Sources(first),
                SecondSources = Sources(second),
                FirstHost = HostFile(first),
                SecondHost = HostFile(second),
            };
        }

        private static string Sources(GeneratorDriverRunResult result)
        {
            var sources = result.Results.Single().GeneratedSources
                .OrderBy(s => s.HintName, StringComparer.Ordinal)
                .Select(s => s.HintName + Environment.NewLine + s.SourceText.ToString());

            var text = string.Join(Environment.NewLine, sources);

            Assert.NotEqual("", text);

            return text;
        }

        private static string HostFile(GeneratorDriverRunResult result)
        {
            var source = result.Results.Single().GeneratedSources
                .Single(s => GeneratorHarness.IsHint(s.HintName, "Host.g.cs"));

            return source.SourceText.ToString();
        }

        #endregion

        #region обход того, что осталось в кэше

        private static readonly Type[] Forbidden =
        {
            typeof(ISymbol),
            typeof(Compilation),
            typeof(SyntaxNode),
            typeof(SyntaxTree),
            typeof(SyntaxReference),
            typeof(SemanticModel),
            typeof(Location),
        };

        private static void AssertNoRoslynState(object? value, HashSet<object> visited, string path)
        {
            if (value is null || value is string || value.GetType().IsPrimitive)
            {
                return;
            }
            if (!visited.Add(value))
            {
                return;
            }

            var type = value.GetType();

            foreach (var forbidden in Forbidden)
            {
                Assert.False(
                    forbidden.IsInstanceOfType(value),
                    $"{path}: в кэше конвейера остался {type.FullName}"
                    );
            }

            if (value is IEnumerable enumerable)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    AssertNoRoslynState(item, visited, $"{path}[{index++}]");
                }

                return;
            }

            //внутрь чужих типов не лезем: смысл обхода - собственная модель генератора,
            //а разворачивать, скажем, ResourceManager из дескриптора диагностики
            //значило бы обходить пол-BCL
            if (type.Assembly != GeneratorAssembly)
            {
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                AssertNoRoslynState(field.GetValue(value), visited, $"{path}.{field.Name}");
            }
        }

        private static readonly Assembly GeneratorAssembly =
            typeof(global::XmlSerDe.Generator.XmlDeserializeGenerator).Assembly;

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        #endregion
    }
}
