#if NETSTANDARD
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Producer;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Вторая половина фасада: сами методы сериализации порождает обычный
    /// <see cref="ClassSourceProducer"/>, а здесь пишется только то, чего у него нет, -
    /// регистрация пар делегатов в реестре.
    ///
    /// Регистрация идёт из <c>[ModuleInitializer]</c>, то есть до первой строки
    /// пользовательского кода. Иначе <c>new XmlSerializer(typeof(T))</c> в статическом
    /// поле успел бы выполниться раньше регистрации и молча ушёл бы в штатный
    /// сериализатор - работало бы, но без ускорения и без единого признака почему.
    /// </summary>
    public static class CompatSourceProducer
    {
        public const string GeneratedNamespace = "XmlSerDe.Compat.Generated";

        //имена нарочно длинные и с префиксом: файл сгенерированного класса
        //кладётся в ту же кучу, что и файлы пользовательских сериализаторов,
        //а совпадение имён там разрешается молчаливым пропуском второго
        public const string SerializerClassName = "XmlSerDeCompatSerializer";
        public const string RegistrationClassName = "XmlSerDeCompatRegistration";

        public static readonly string SerializerFullName = "global::" + GeneratedNamespace + "." + SerializerClassName;

        public const string RegistryFullName = "global::XmlSerDe.Compat.XmlSerDeRegistry";
        public const string PooledCharExhausterFullName = "global::XmlSerDe.Components.Exhauster.PooledCharExhauster";
        public const string LengthEstimatorExhausterFullName = "global::XmlSerDe.Components.Exhauster.LengthEstimatorExhauster";
        public const string Utf8StreamExhausterFullName = "global::XmlSerDe.Components.Exhauster.Utf8StreamExhauster";
        public const string InjectorFullName = "global::XmlSerDe.Components.Injector.DefaultInjector";

        /// <summary>
        /// Состав, который скармливается <see cref="ClassSourceProducer"/>: тот же
        /// самый, что построили бы атрибуты на руками написанном классе, только
        /// собранный обходом графа.
        /// </summary>
        public static SerializationInfoCollection BuildCollection(
            Compilation compilation,
            IReadOnlyList<INamedTypeSymbol> roots,
            IReadOnlyList<INamedTypeSymbol> subjects
            )
        {
            var rootNames = new HashSet<string>();
            foreach (var root in roots)
            {
                rootNames.Add(root.ToGlobalDisplayString());
            }

            var sinfos = new Dictionary<string, SerializationInfo>();
            foreach (var subject in subjects)
            {
                var name = subject.ToGlobalDisplayString();
                if (sinfos.ContainsKey(name))
                {
                    continue;
                }

                sinfos[name] = new SerializationInfo(subject, rootNames.Contains(name));
            }

            ClassSourceProducer.ExpandXmlIncludes(sinfos);

            var infos = new List<SerializationInfo>(sinfos.Values);

            //три стока: оценщик и pooled char для string/TextWriter, UTF-8 для Stream.
            //Размер документа в SerializeToString заранее неизвестен.
            return new SerializationInfoCollection(
                new List<INamedTypeSymbol>
                {
                    compilation.LengthEstimatorExhauster(),
                    compilation.PooledCharExhauster(),
                    compilation.Utf8StreamExhauster(),
                },
                new List<INamedTypeSymbol> { compilation.DefaultInjector(), },
                infos
                );
        }

        public static string GenerateRegistration(
            IReadOnlyList<INamedTypeSymbol> roots
            )
        {
            var sb = new StringBuilder();

            sb.AppendLine($$"""
using roschar = System.ReadOnlySpan<char>;

namespace {{GeneratedNamespace}}
{
    /// <summary>
    /// Сгенерировано по точкам вызова new XmlSerializer(typeof(T)).
    /// </summary>
    internal static class {{RegistrationClassName}}
    {
        [global::System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
""");

            foreach (var root in roots)
            {
                var global = root.ToGlobalDisplayString();

                sb.AppendLine($$"""
            {{RegistryFullName}}.Register(
                typeof({{global}}),
                (exhauster, obj, appendXmlHead) =>
                {
                    var typed = ({{global}})obj;
                    if (exhauster is {{PooledCharExhausterFullName}} pooled)
                    {
                        {{SerializerFullName}}.{{ClassSourceProducer.HeadSerializeMethodName}}(pooled, typed, appendXmlHead);
                        return;
                    }
                    if (exhauster is {{LengthEstimatorExhausterFullName}} estimator)
                    {
                        {{SerializerFullName}}.{{ClassSourceProducer.HeadSerializeMethodName}}(estimator, typed, appendXmlHead);
                        return;
                    }
                    if (exhauster is {{Utf8StreamExhausterFullName}} utf8)
                    {
                        {{SerializerFullName}}.{{ClassSourceProducer.HeadSerializeMethodName}}(utf8, typed, appendXmlHead);
                        return;
                    }
                    throw new global::System.InvalidCastException(
                        "XmlSerDe.Compat serialize path supports PooledCharExhauster, LengthEstimatorExhauster and Utf8StreamExhauster only."
                        );
                },
                (roschar xml) =>
                {
                    {{SerializerFullName}}.{{ClassSourceProducer.HeadDeserializeMethodName}}({{InjectorFullName}}.Instance, xml, out {{global}} result);
                    return result;
                },
                "{{root.GetXmlRootName()}}"
                );
""");
            }

            sb.AppendLine($$"""
        }
    }
}
""");

            return sb.ToString();
        }

        /// <summary>
        /// <c>[ModuleInitializer]</c> распознаётся компилятором по имени, а не по
        /// сборке, в которой объявлен, - поэтому на таргетах, где его в рантайме нет,
        /// его достаточно объявить рядом. На net5.0+ он есть, и второе объявление
        /// сделало бы имя неоднозначным, отсюда условная компиляция.
        /// </summary>
        public static string GenerateModuleInitializerPolyfill()
        {
            return """
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : global::System.Attribute
    {
    }
}
#endif
""";
        }
    }
}
#endif
