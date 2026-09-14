using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using XmlSerDe.Generator.Producer;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Контракт стока и инжектора: генератор требует наследования от
    /// <c>ExhausterBase</c> / <c>InjectorBase</c>, а не реализации интерфейса.
    ///
    /// Зачем именно класс, написано в шапке самих баз: у интерфейса новый член
    /// ломает всех, кто его реализовал, а у класса приезжает virtual'ом с телом
    /// по умолчанию. Тест сторожит именно это - что голой реализации интерфейса
    /// генератор больше не принимает и что сообщение объясняет причину.
    ///
    /// Второе, что здесь проверяется, - предупреждение XMLSERDE010 о
    /// незапечатанном стоке: цена измерена в docs/dispatch-cost.md.
    /// </summary>
    public class SinkContractFixture
    {
        private const string Subjects = @"
namespace Sample
{
    public class Node
    {
        public int Number { get; set; }
    }
}
";

        /// <summary>
        /// Экзостер, реализующий интерфейс напрямую. Членов у него нет вовсе -
        /// до проверки полноты дело не доходит, генератор отказывает раньше.
        /// </summary>
        private const string BareInterfaceExhauster = @"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    public sealed partial class BareExhauster : IExhauster
    {
    }
}
";

        [Fact]
        public void Exhauster_ImplementingInterfaceOnly_IsRejected()
        {
            var diagnostics = Run(BareInterfaceExhauster + @"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(BareExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class BareHost
    {
    }
}
");

            var message = SingleGeneratorFailure(diagnostics);

            Assert.Contains(ClassSourceProducer.ExhausterBaseFullName, message, StringComparison.Ordinal);
            //сообщение обязано объяснять, почему интерфейса мало, а не просто
            //констатировать отказ
            Assert.Contains("default implementation", message, StringComparison.Ordinal);
        }

        [Fact]
        public void Injector_ImplementingInterfaceOnly_IsRejected()
        {
            var diagnostics = Run(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    public sealed class BareInjector : IInjector
    {
    }

    [XmlInjector(typeof(BareInjector))]
    [XmlSubject(typeof(Node), true)]
    public partial class BareInjectorHost
    {
    }
}
");

            var message = SingleGeneratorFailure(diagnostics);

            Assert.Contains(ClassSourceProducer.InjectorBaseFullName, message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Штатный сток наследуется от базы, и хост на нём генерируется молча:
        /// ни отказа, ни предупреждения о sealed - <c>StringBuilderExhauster</c>
        /// запечатан.
        /// </summary>
        [Fact]
        public void SealedShippedExhauster_GeneratesWithoutDiagnostics()
        {
            var diagnostics = Run(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class SealedHost
    {
    }
}
");

            Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        }

        /// <summary>
        /// Незапечатанный сток из этой же сборки - предупреждение, а не отказ:
        /// код работает, просто вызов не девиртуализуется.
        /// </summary>
        [Fact]
        public void UnsealedExhauster_Warns()
        {
            var diagnostics = Run(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    public class OpenExhauster : StringBuilderExhausterStub
    {
    }

    [XmlExhauster(typeof(OpenExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class OpenHost
    {
    }
}
".Replace("StringBuilderExhausterStub", "global::XmlSerDe.Tests.Dispatch.ThinBase"));

            var warning = Assert.Single(
                diagnostics.Where(d => d.Id == SinkDiagnostics.NotSealedId)
                );

            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("OpenExhauster", warning.GetMessage(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Чужой незапечатанный тип молчит: запечатать его из этой компиляции
        /// всё равно нельзя. Проверяется на <c>DefaultInjector</c> - он открыт
        /// намеренно, и именно он приезжает хосту по умолчанию.
        /// </summary>
        [Fact]
        public void UnsealedTypeFromAnotherAssembly_DoesNotWarn()
        {
            var diagnostics = Run(@"
using XmlSerDe;
using XmlSerDe.Internal;

namespace Sample
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(Node), true)]
    public partial class DefaultInjectorHost
    {
    }
}
");

            Assert.Empty(diagnostics.Where(d => d.Id == SinkDiagnostics.NotSealedId));
        }

        /// <summary>
        /// Отказ генератора приезжает единственной ошибкой с id XmlSerDe -
        /// так устроена обёртка исключений в XmlDeserializeGenerator.
        /// </summary>
        private static string SingleGeneratorFailure(Diagnostic[] diagnostics)
        {
            var failure = Assert.Single(
                diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                );

            return failure.GetMessage();
        }

        private static Diagnostic[] Run(string hostSource)
        {
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(Subjects, path: "subject.cs"),
                CSharpSyntaxTree.ParseText(hostSource, path: "host.cs"),
            };

            var compilation = CSharpCompilation.Create(
                "SinkContractFixtureAssembly",
                trees,
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), }
                );

            return driver.RunGenerators(compilation).GetRunResult().Diagnostics.ToArray();
        }
    }
}
