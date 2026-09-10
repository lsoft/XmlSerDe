#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Incremental;
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
            EquatableArray<CompatCallSiteRef> callSites,
            string? strictOption
            )
        {
            if (callSites.Count == 0)
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
                adder.Report(
                    new DiagnosticInfo(
                        CompatDiagnostics.GenerationFailed(
                            severity == DiagnosticSeverity.Info ? DiagnosticSeverity.Warning : severity
                            ),
                        location: null,
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
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{exhaustType.ToMetadataName()}.g.cs",
                    bsg.GenerateSerializationBody(exhaustType)
                    );
            }
            foreach (var injectorType in sources.Collection.InjectorList)
            {
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{injectorType.ToMetadataName()}.g.cs",
                    bsg.GenerateDeserializationBody(injectorType)
                    );
            }

            adder.AddDocumentToCompilation(
                "XmlSerDe.Compat.Serializer.g.cs",
                sources.SerializerClass
                );
            adder.AddDocumentToCompilation(
                "XmlSerDe.Compat.Registration.g.cs",
                sources.Registration
                );
            adder.AddDocumentToCompilation(
                "XmlSerDe.ModuleInitializerAttribute.g.cs",
                CompatSourceProducer.GenerateModuleInitializerPolyfill()
                );
        }

        private static void Report(
            XmlDeserializeGenerator.DocumentAdder adder,
            DiagnosticSeverity severity,
            CompatRoot root,
            string refusal
            )
        {
            adder.Report(
                new DiagnosticInfo(
                    CompatDiagnostics.NotAccelerated(severity),
                    root.Location,
                    root.Type.ToGlobalDisplayString(),
                    refusal
                    )
                );
        }

        /// <summary>
        /// Точки вызова приходят уже без повторов - одна на тип, - но названный там
        /// тип надо разрешить заново: по конвейеру ехало его имя, а не символ,
        /// и символ должен быть от нынешней компиляции, а не от позапрошлой.
        ///
        /// Имя может и не разрешиться: тип успели переименовать или удалить, а точка
        /// вызова осталась от дерева, которое с тех пор не трогали. Тогда молчим -
        /// в компиляции и без нас есть ошибка про несуществующий тип.
        /// </summary>
        private static List<CompatRoot> CollectRoots(
            Compilation compilation,
            EquatableArray<CompatCallSiteRef> callSites
            )
        {
            var result = new List<CompatRoot>();

            foreach (var callSite in callSites)
            {
                var type = compilation.ResolveByMetadataName(callSite.TypeMetadataName);
                if (type is null)
                {
                    continue;
                }

                result.Add(new CompatRoot(type, callSite.Location));
            }

            return result;
        }

        private readonly struct CompatRoot
        {
            public readonly INamedTypeSymbol Type;
            public readonly LocationInfo? Location;

            public CompatRoot(INamedTypeSymbol type, LocationInfo? location)
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
