#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using XmlSerDe.Generator.Producer;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Compat;
using XmlSerDe.Generator.Incremental;
using XmlSerDe.Common;

namespace XmlSerDe.Generator
{
    [Generator(LanguageNames.CSharp)]
    public class XmlDeserializeGenerator : IIncrementalGenerator
    {
        public static readonly string SubjectAttributeFullName = typeof(XmlSubjectAttribute).FullName;
        public static readonly string FactoryAttributeFullName = typeof(XmlFactoryAttribute).FullName;
        public static readonly string ExhausterAttributeFullName = typeof(XmlExhausterAttribute).FullName;
        public static readonly string InjectorAttributeFullName = typeof(XmlInjectorAttribute).FullName;
        public static readonly string FeaturesAttributeFullName = typeof(XmlFeaturesAttribute).FullName;
        public static readonly string GuardsAttributeFullName = typeof(XmlGuardsAttribute).FullName;

        internal const string HostsTrackingName = "Hosts";
        internal const string CallSitesTrackingName = "CallSites";
        internal const string StrictTrackingName = "Strict";
        internal const string GenerateTrackingName = "Generate";

        /// <summary>
        /// Про инкрементальность здесь всё держится на одном решении, и оно не совсем
        /// такое, как советуют обычно.
        ///
        /// Обычный совет - «не таскайте <see cref="Compilation"/> по конвейеру, стройте
        /// генерацию от размеченного объявления» - написан для генераторов, чей вывод
        /// зависит только от самого этого объявления. Здесь не так: <c>[XmlSubject]</c>
        /// висит на классе-хосте, а порождается код по <b>транзитивному замыканию графа
        /// типов</b>, и типы эти лежат в других файлах. Roslyn перезапускает transform
        /// синтаксического провайдера только для изменившихся деревьев, поэтому генератор,
        /// заведённый строго от узла хоста, был бы прекрасно инкрементален и при этом
        /// выдавал бы устаревший код при правке поля в соседнем файле.
        ///
        /// Поэтому <see cref="Compilation"/> остаётся входом, и связывание символов честно
        /// гоняется на каждую правку. Кэш берётся не из триггера, а из равенства выходов:
        /// шаг <see cref="GenerateTrackingName"/> отдаёт <see cref="GenerationResult"/> -
        /// строки и диагностику, без единого символа, - и если результат совпал с прошлым,
        /// выходной шаг помечается Cached. А это и есть главная цена: не работа самого
        /// генератора, а повторный разбор сгенерированных деревьев и всё, что от них
        /// зависит.
        /// </summary>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //ForAttributeWithMetadataName вместо CreateSyntaxProvider: у Roslyn есть
            //индекс по именам атрибутов, и до predicate доходят только те классы,
            //где нужный атрибут действительно написан, а не любые размеченные вообще
            IncrementalValueProvider<EquatableArray<string>> hosts = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    SubjectAttributeFullName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => GetHostMetadataName(ctx))
                .Where(static name => name is not null)
                .Collect()
                .Select(static (names, _) => SortedDistinct(names!))
                .WithTrackingName(HostsTrackingName);

            //точки вызова фасада совместимости - new XmlSerializer(typeof(T)),
            //XmlSerializer.FromTypes(...), factory.CreateSerializer(typeof(T)).
            //Проект, не подключивший XmlSerDe.Compat, не платит за это ничего:
            //ни один узел не пройдёт проверку имени типа в transform
            IncrementalValueProvider<EquatableArray<CompatCallSiteRef>> compatCallSites = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => CompatCallSiteCollector.IsCandidate(s),
                    transform: static (ctx, _) => GetCompatCallSites(ctx))
                .SelectMany(static (sites, _) => sites)
                .Collect()
                .Select(static (sites, _) => SortedDistinct(sites))
                .WithTrackingName(CallSitesTrackingName);

            var strict = context.AnalyzerConfigOptionsProvider.Select(
                static (provider, _) =>
                {
                    provider.GlobalOptions.TryGetValue(CompatDiagnostics.StrictPropertyName, out var value);
                    return value;
                })
                .WithTrackingName(StrictTrackingName);

            //Compilation подключается последним и намеренно: он меняется на каждое
            //нажатие клавиши, поэтому всё, что ниже, обязано быть сравнимым по значению
            var generation = hosts
                .Combine(compatCallSites)
                .Combine(strict)
                .Combine(context.CompilationProvider)
                .Select(static (source, ct) => Build(
                    source.Right,
                    source.Left.Left.Left,
                    source.Left.Left.Right,
                    source.Left.Right,
                    ct
                    ))
                .WithTrackingName(GenerateTrackingName);

            context.RegisterSourceOutput(
                generation,
                static (spc, result) => Emit(spc, result)
                );
        }

        /// <summary>
        /// Единственное, что делает выходной шаг: перекладывает уже готовые строки.
        /// Ни символов, ни компиляции здесь нет и быть не должно.
        /// </summary>
        private static void Emit(
            SourceProductionContext context,
            GenerationResult result
            )
        {
            foreach (var source in result.Sources)
            {
                context.AddSource(
                    source.HintName,
                    SourceText.From(source.Text, Encoding.UTF8)
                    );
            }

            foreach (var diagnostic in result.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        }

        private static GenerationResult Build(
            Compilation compilation,
            EquatableArray<string> hostNames,
            EquatableArray<CompatCallSiteRef> compatCallSites,
            string? strictOption,
            CancellationToken cancellationToken
            )
        {
            var adder = new DocumentAdder(cancellationToken);

            try
            {
                var bsg = new BuiltinSourceProducer(
                    compilation
                    );
                var bsgMainPart = bsg.GenerateMainPart(
                    );
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.MainPart.g.cs",
                    bsgMainPart
                    );
                var bsgSerializationSharedBody = bsg.GenerateSerializationSharedBody(
                    );
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.Serialization.Shared.g.cs",
                    bsgSerializationSharedBody
                    );

                GenerateSources(adder, compilation, hostNames);

                CompatGenerator.Generate(adder, compilation, compatCallSites, strictOption);
            }
            catch (OperationCanceledException)
            {
                //отмена - это не ошибка генерации, и объявлять её ошибкой сборки нельзя:
                //в IDE отменяется каждая вторая правка
                throw;
            }
            catch (Exception excp)
            {
                var msg = (excp.Message + Environment.NewLine + excp.StackTrace)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ');

                //то, что успело сгенерироваться до падения, остаётся: так было и раньше,
                //когда AddSource звался по ходу дела
                adder.Report(
                    new DiagnosticInfo(
                        new DiagnosticDescriptor(
                            id: "XmlSerDe",
                            title: msg,
                            messageFormat: msg,
                            category: "XmlSerDe",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true
                            ),
                        location: null
                        )
                    );
            }

            return adder.ToResult();
        }

        private static void GenerateSources(
            DocumentAdder adder,
            Compilation compilation,
            EquatableArray<string> hostNames
            )
        {
            if (hostNames.Count == 0)
            {
                // nothing to do yet
                return;
            }

            // Convert each host name back to a symbol of the current compilation
            var classesToGenerate = GetClassesToGenerate(compilation, hostNames, adder.CancellationToken);

            // If there were errors in the host declaration, we won't create an
            // ClassesToGenerate for it, so make sure we have something to generate
            if (classesToGenerate.Count > 0)
            {
                foreach (var ctgs in classesToGenerate)
                {
                    if (!ctgs.IsPartial())
                    {
                        throw new InvalidOperationException($"Class {ctgs.ToFullDisplayString()} should be partial");
                    }

                    var sp = new ClassSourceProducer(
                        compilation,
                        ctgs
                        );

                    ReportUnsealedSinks(adder, compilation, sp.SerializationInfoCollection);

                    //generate builtin source
                    var bsg = new BuiltinSourceProducer(
                        compilation
                        );
                    foreach (var exhaustType in sp.SerializationInfoCollection.ExhaustList)
                    {
                        var bsgBody = bsg.GenerateSerializationBody(
                            exhaustType
                            );
                        adder.AddDocumentToCompilation(
                            $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{exhaustType.ToMetadataName()}.g.cs",
                            bsgBody
                            );
                    }
                    foreach (var injectorType in sp.SerializationInfoCollection.InjectorList)
                    {
                        var bsgBody = bsg.GenerateDeserializationBody(
                            injectorType
                            );
                        adder.AddDocumentToCompilation(
                            $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{injectorType.ToMetadataName()}.g.cs",
                            bsgBody
                            );
                    }

                    var source = sp.GenerateClass();

                    //имя файла - полное имя типа: два одноимённых хоста из разных
                    //пространств имён по простому имени сливались в один файл, и второй
                    //молча оставался без сгенерированной половины
                    adder.AddDocumentToCompilation(
                        $"{ctgs.ToMetadataName()}.g.cs",
                        source
                        );

                }
            }
        }

        /// <summary>
        /// Незапечатанный сток - не ошибка, а упущенная девиртуализация: цена
        /// померена в docs/dispatch-cost.md. Абстрактные базы пропускаются -
        /// запечатать их нельзя по определению, а регистрируют их только
        /// осознанно.
        /// </summary>
        private static void ReportUnsealedSinks(
            DocumentAdder adder,
            Compilation compilation,
            SerializationInfoCollection collection
            )
        {
            foreach (var type in collection.ExhaustList)
            {
                ReportIfUnsealed(adder, compilation, type, "exhauster");
            }

            foreach (var type in collection.InjectorList)
            {
                ReportIfUnsealed(adder, compilation, type, "injector");
            }
        }

        private static void ReportIfUnsealed(
            DocumentAdder adder,
            Compilation compilation,
            INamedTypeSymbol type,
            string role
            )
        {
            if (type.IsSealed || type.IsAbstract)
            {
                return;
            }

            //чужой тип запечатать отсюда всё равно нельзя, а предупреждать о
            //нём на каждом хосте значило бы приучить глушить диагностику
            //целиком. Заодно это снимает шум с наших собственных типов:
            //DefaultInjector открыт затем, чтобы от него наследовались ради
            //одной перегрузки, Utf8StreamExhauster - чтобы подставить свой сток
            //байтов, и то и другое - решение, а не упущение
            if (!SymbolEqualityComparer.Default.Equals(
                    type.ContainingAssembly,
                    compilation.Assembly
                    ))
            {
                return;
            }

            adder.Report(
                new DiagnosticInfo(
                    SinkDiagnostics.NotSealed,
                    location: null,
                    type.ToFullDisplayString(),
                    role
                    )
                );
        }

        private static List<INamedTypeSymbol> GetClassesToGenerate(
            Compilation compilation,
            EquatableArray<string> hostNames,
            CancellationToken ct
            )
        {
            // Create a list to hold our output
            var result = new List<INamedTypeSymbol>();

            foreach (var hostName in hostNames)
            {
                // stop if we're asked to
                ct.ThrowIfCancellationRequested();

                var classSymbol = compilation.ResolveByMetadataName(hostName);
                if (classSymbol is null)
                {
                    // the host is gone from this compilation, bail out
                    continue;
                }

                result.Add(classSymbol);
            }

            return result;
        }

        /// <summary>
        /// Типов на одну точку вызова бывает больше одного: <c>FromTypes</c>
        /// принимает массив. Поэтому transform отдаёт массив, а не одну ссылку -
        /// пустой там, где узел оказался чужим.
        /// </summary>
        private static ImmutableArray<CompatCallSiteRef> GetCompatCallSites(GeneratorSyntaxContext context)
        {
            var types = CompatCallSiteCollector.CollectRootTypes(context.SemanticModel, context.Node);
            if (types.Count == 0)
            {
                return ImmutableArray<CompatCallSiteRef>.Empty;
            }

            var location = LocationInfo.From(context.Node);

            var builder = ImmutableArray.CreateBuilder<CompatCallSiteRef>(types.Count);
            foreach (var type in types)
            {
                builder.Add(
                    new CompatCallSiteRef(
                        type.ToMetadataName(),
                        location
                        )
                    );
            }

            return builder.MoveToImmutable();
        }

        private static string? GetHostMetadataName(GeneratorAttributeSyntaxContext context)
        {
            return context.TargetSymbol is INamedTypeSymbol nts
                ? nts.ToMetadataName()
                : null;
        }

        /// <summary>
        /// Состав, отсортированный и без повторов. Порядок обнаружения не годится:
        /// он меняется от правок, к генерации отношения не имеющих, и кэш из-за этого
        /// не срабатывал бы. Повторы тут обычное дело - хотя бы partial-объявления
        /// одного и того же класса-хоста.
        /// </summary>
        private static EquatableArray<string> SortedDistinct(ImmutableArray<string> names)
        {
            if (names.IsDefaultOrEmpty)
            {
                return EquatableArray<string>.Empty;
            }

            var sorted = names.ToArray();
            Array.Sort(sorted, StringComparer.Ordinal);

            var result = new List<string>(sorted.Length);
            foreach (var name in sorted)
            {
                if (result.Count == 0 || !string.Equals(result[result.Count - 1], name, StringComparison.Ordinal))
                {
                    result.Add(name);
                }
            }

            return EquatableArray.From(result);
        }

        private static EquatableArray<CompatCallSiteRef> SortedDistinct(ImmutableArray<CompatCallSiteRef> callSites)
        {
            if (callSites.IsDefaultOrEmpty)
            {
                return EquatableArray<CompatCallSiteRef>.Empty;
            }

            var sorted = callSites.ToArray();
            Array.Sort(sorted, CompatCallSiteRef.Compare);

            //один и тот же тип называют в нескольких местах; дальше по конвейеру
            //нужна одна точка на тип - та, на которую вешается диагностика отказа
            var result = new List<CompatCallSiteRef>(sorted.Length);
            foreach (var callSite in sorted)
            {
                if (result.Count == 0
                    || !string.Equals(result[result.Count - 1].TypeMetadataName, callSite.TypeMetadataName, StringComparison.Ordinal))
                {
                    result.Add(callSite);
                }
            }

            return EquatableArray.From(result);
        }

        /// <summary>
        /// Копилка сгенерированного, а не проводник к компилятору: настоящий
        /// <c>AddSource</c> случится в выходном шаге, если результат вообще
        /// окажется новым. Дедупликация по имени файла здесь и живёт - основная
        /// ветка и фасад совместимости порождают общий <c>BuiltinCodeHelper</c>.
        /// </summary>
        internal sealed class DocumentAdder
        {
            private readonly HashSet<string> _filePaths = new HashSet<string>();
            private readonly List<GeneratedSource> _sources = new List<GeneratedSource>();
            private readonly List<DiagnosticInfo> _diagnostics = new List<DiagnosticInfo>();

            public readonly CancellationToken CancellationToken;

            public DocumentAdder(
                CancellationToken cancellationToken
                )
            {
                CancellationToken = cancellationToken;
            }

            public void AddDocumentToCompilation(
                string documentName,
                string document
                )
            {
                if (string.IsNullOrEmpty(documentName))
                {
                    throw new ArgumentException($"'{nameof(documentName)}' cannot be null or empty.", nameof(documentName));
                }

                if (document is null)
                {
                    throw new ArgumentNullException(nameof(document));
                }

                if (!_filePaths.Add(documentName))
                {
                    return;
                }

                _sources.Add(
                    new GeneratedSource(
                        documentName,
                        BlankOutWhitespaceOnlyLines(document)
                        )
                    );
            }

            public void Report(DiagnosticInfo diagnostic)
            {
                _diagnostics.Add(diagnostic);
            }

            public GenerationResult ToResult()
            {
                if (_sources.Count == 0 && _diagnostics.Count == 0)
                {
                    return GenerationResult.Empty;
                }

                return new GenerationResult(
                    EquatableArray.From(_sources),
                    EquatableArray.From(_diagnostics)
                    );
            }

            /// <summary>
            /// Строка из одних пробелов превращается в пустую.
            ///
            /// Такие строки появляются там, где в шаблон подставляется условный
            /// фрагмент, которого в этом конкретном случае нет: отступ вокруг него
            /// в шаблоне записан, а подставлять в него нечего. Смысла у них никакого,
            /// а читать сгенерированный код приходится - в него заходят отладчиком.
            ///
            /// Строка целиком из пробелов не может оказаться внутри строкового
            /// литерала: генератор не порождает многострочных литералов вовсе.
            /// </summary>
            private static string BlankOutWhitespaceOnlyLines(string source)
            {
                var lines = source.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimEnd();
                    if (trimmed.Length == 0 && line.Length > 0)
                    {
                        //перенос строки, если он тут был, надо сохранить
                        lines[i] = line.EndsWith("\r") ? "\r" : "";
                    }
                }

                return string.Join("\n", lines);
            }
        }
    }
}
#endif
