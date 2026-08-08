#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Producer;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Генерация фасада целиком: от точек вызова до зарегистрированных делегатов.
    ///
    /// Живёт внутри того же <see cref="XmlDeserializeGenerator"/>, а не отдельным
    /// генератором, по одной причине: <c>BuiltinCodeHelper</c> - partial-класс, общий
    /// на всю сборку, и два независимых генератора объявили бы его дважды. Общий
    /// <see cref="XmlDeserializeGenerator.DocumentAdder"/> склеивает их по имени файла.
    /// </summary>
    internal static class CompatGenerator
    {
        internal static void Generate(
            XmlDeserializeGenerator.DocumentAdder adder,
            Compilation compilation,
            ImmutableArray<ObjectCreationExpressionSyntax> callSites,
            string? strictOption
            )
        {
            if (callSites.IsDefaultOrEmpty)
            {
                //фасад не подключён или ни одного new XmlSerializer(typeof(T)) нет:
                //не порождаем ни строки
                return;
            }

            var severity = CompatDiagnostics.ParseStrict(strictOption);

            var roots = CollectRoots(compilation, callSites);
            if (roots.Count == 0)
            {
                return;
            }

            var accepted = new List<INamedTypeSymbol>();
            var subjects = new List<INamedTypeSymbol>();
            var subjectNames = new HashSet<string>();

            foreach (var root in roots)
            {
                if (!CompatGraphWalker.TryWalk(compilation, root.Type, out var rootSubjects, out var refusal))
                {
                    Report(adder, severity, root, refusal);
                    continue;
                }

                accepted.Add(root.Type);

                foreach (var subject in rootSubjects)
                {
                    if (subjectNames.Add(subject.ToGlobalDisplayString()))
                    {
                        subjects.Add(subject);
                    }
                }
            }

            if (accepted.Count == 0)
            {
                return;
            }

            if (!TryEmit(adder, compilation, accepted, subjects, out var failure))
            {
                //обходчик пропустил что-то, чего продюсер всё-таки не умеет. Это пробел
                //в его белом списке, а не отказ по замыслу, поэтому видно всегда
                adder.Context.ReportDiagnostic(
                    Diagnostic.Create(
                        CompatDiagnostics.GenerationFailed(
                            severity == DiagnosticSeverity.Info ? DiagnosticSeverity.Warning : severity
                            ),
                        Location.None,
                        failure
                        )
                    );
            }
        }

        /// <summary>
        /// Сначала пробуем весь состав разом - в обычном случае этим всё и кончается.
        /// Если продюсер отказал, ищем виноватых поштучно и пробуем ещё раз без них:
        /// один неудачный тип не должен лишать ускорения всё остальное.
        /// </summary>
        private static bool TryEmit(
            XmlDeserializeGenerator.DocumentAdder adder,
            Compilation compilation,
            List<INamedTypeSymbol> roots,
            List<INamedTypeSymbol> subjects,
            out string failure
            )
        {
            failure = "";

            if (TryProduce(compilation, roots, subjects, out var sources, out var firstFailure))
            {
                AddSources(adder, compilation, sources);
                return true;
            }

            var bad = new HashSet<string>();
            foreach (var subject in subjects)
            {
                if (!TryProduce(compilation, new List<INamedTypeSymbol>(), new List<INamedTypeSymbol> { subject, }, out _, out _))
                {
                    bad.Add(subject.ToGlobalDisplayString());
                }
            }

            if (bad.Count == 0)
            {
                failure = firstFailure;
                return false;
            }

            var survivingRoots = new List<INamedTypeSymbol>();
            var survivingSubjects = new List<INamedTypeSymbol>();
            foreach (var root in roots)
            {
                if (!CompatGraphWalker.TryWalk(compilation, root, out var rootSubjects, out _))
                {
                    continue;
                }
                if (rootSubjects.Exists(s => bad.Contains(s.ToGlobalDisplayString())))
                {
                    continue;
                }

                survivingRoots.Add(root);
                survivingSubjects.AddRange(rootSubjects);
            }

            if (survivingRoots.Count == 0)
            {
                failure = firstFailure;
                return false;
            }

            if (!TryProduce(compilation, survivingRoots, Distinct(survivingSubjects), out sources, out var secondFailure))
            {
                failure = secondFailure;
                return false;
            }

            AddSources(adder, compilation, sources);
            return true;
        }

        private static List<INamedTypeSymbol> Distinct(List<INamedTypeSymbol> types)
        {
            var names = new HashSet<string>();
            var result = new List<INamedTypeSymbol>();

            foreach (var type in types)
            {
                if (names.Add(type.ToGlobalDisplayString()))
                {
                    result.Add(type);
                }
            }

            return result;
        }

        private static bool TryProduce(
            Compilation compilation,
            List<INamedTypeSymbol> roots,
            List<INamedTypeSymbol> subjects,
            out CompatSources sources,
            out string failure
            )
        {
            sources = default;
            failure = "";

            try
            {
                var collection = CompatSourceProducer.BuildCollection(compilation, roots, subjects);

                var producer = new ClassSourceProducer(
                    compilation,
                    CompatSourceProducer.GeneratedNamespace,
                    CompatSourceProducer.SerializerClassName,
                    new[] { "using System;", },
                    collection
                    );

                sources = new CompatSources(
                    producer.GenerateClass(),
                    CompatSourceProducer.GenerateRegistration(roots),
                    collection
                    );

                return true;
            }
            catch (Exception excp)
            {
                failure = excp.Message.Replace('\r', ' ').Replace('\n', ' ');
                return false;
            }
        }

        private static void AddSources(
            XmlDeserializeGenerator.DocumentAdder adder,
            Compilation compilation,
            CompatSources sources
            )
        {
            var bsg = new BuiltinSourceProducer(compilation);

            foreach (var exhaustType in sources.Collection.ExhaustList)
            {
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{exhaustType.Name}.g.cs",
                    SourceText.From(bsg.GenerateSerializationBody(exhaustType), Encoding.UTF8)
                    );
            }
            foreach (var injectorType in sources.Collection.InjectorList)
            {
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{injectorType.Name}.g.cs",
                    SourceText.From(bsg.GenerateDeserializationBody(injectorType), Encoding.UTF8)
                    );
            }

            adder.AddDocumentToCompilation(
                "XmlSerDe.Compat.Serializer.g.cs",
                SourceText.From(sources.SerializerClass, Encoding.UTF8)
                );
            adder.AddDocumentToCompilation(
                "XmlSerDe.Compat.Registration.g.cs",
                SourceText.From(sources.Registration, Encoding.UTF8)
                );
            adder.AddDocumentToCompilation(
                "XmlSerDe.ModuleInitializerAttribute.g.cs",
                SourceText.From(CompatSourceProducer.GenerateModuleInitializerPolyfill(), Encoding.UTF8)
                );
        }

        private static void Report(
            XmlDeserializeGenerator.DocumentAdder adder,
            DiagnosticSeverity severity,
            CompatRoot root,
            string refusal
            )
        {
            adder.Context.ReportDiagnostic(
                Diagnostic.Create(
                    CompatDiagnostics.NotAccelerated(severity),
                    root.Location,
                    root.Type.ToGlobalDisplayString(),
                    refusal
                    )
                );
        }

        /// <summary>
        /// Один и тот же тип обычно называют в нескольких точках вызова; для отказа
        /// берётся первая - показывать одно и то же в каждой было бы шумом.
        /// </summary>
        private static List<CompatRoot> CollectRoots(
            Compilation compilation,
            ImmutableArray<ObjectCreationExpressionSyntax> callSites
            )
        {
            var seen = new HashSet<string>();
            var result = new List<CompatRoot>();

            foreach (var callSite in callSites)
            {
                var semanticModel = compilation.GetSemanticModel(callSite.SyntaxTree);

                var type = CompatCallSiteCollector.TryGetRootType(semanticModel, callSite);
                if (type is null)
                {
                    continue;
                }
                if (!seen.Add(type.ToGlobalDisplayString()))
                {
                    continue;
                }

                result.Add(new CompatRoot(type, callSite.GetLocation()));
            }

            return result;
        }

        private readonly struct CompatRoot
        {
            public readonly INamedTypeSymbol Type;
            public readonly Location Location;

            public CompatRoot(INamedTypeSymbol type, Location location)
            {
                Type = type;
                Location = location;
            }
        }

        private readonly struct CompatSources
        {
            public readonly string SerializerClass;
            public readonly string Registration;
            public readonly SerializationInfoCollection Collection;

            public CompatSources(
                string serializerClass,
                string registration,
                SerializationInfoCollection collection
                )
            {
                SerializerClass = serializerClass;
                Registration = registration;
                Collection = collection;
            }
        }
    }
}
#endif
