using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Общая обвязка для тестов, гоняющих генератор напрямую через
    /// <c>CSharpGeneratorDriver</c>: ссылки и опции сборки.
    /// </summary>
    internal static class GeneratorHarness
    {
        /// <summary>
        /// Ссылки берутся у самой тестовой сборки: она уже ссылается ровно на то,
        /// что нужно проверяемому коду, - Common, Components и Compat, - и повторять
        /// этот список вручную значило бы завести второе место, где он может
        /// разойтись с настоящим.
        /// </summary>
        public static IEnumerable<MetadataReference> References()
        {
            //без обращения к типу сборка может быть ещё не загружена
            _ = typeof(XmlSerDe.Common.IExhauster).FullName;
            _ = typeof(XmlSerDe.Components.Injector.DefaultInjector).FullName;
            _ = typeof(XmlSerDe.Compat.XmlSerializer).FullName;
            _ = typeof(System.Xml.Serialization.XmlRootAttribute).FullName;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .ToList();
        }

        public sealed class StrictOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _options;

            public StrictOptionsProvider(string? strict)
            {
                _options = new Options(strict);
            }

            public override AnalyzerConfigOptions GlobalOptions => _options;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

            private sealed class Options : AnalyzerConfigOptions
            {
                private readonly string? _strict;

                public Options(string? strict)
                {
                    _strict = strict;
                }

                public override bool TryGetValue(string key, out string value)
                {
                    if (key == "build_property.XmlSerDeCompatStrict" && _strict is not null)
                    {
                        value = _strict;
                        return true;
                    }

                    value = null!;
                    return false;
                }
            }
        }
    }
}
