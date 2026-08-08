#if NETSTANDARD
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Отказ фасада ничего не ломает - тип просто уходит штатному
    /// <see cref="System.Xml.Serialization.XmlSerializer"/>. Но молчаливый отказ
    /// оставил бы вопрос «почему у меня не ускорилось» без единой зацепки, поэтому
    /// каждый отказ виден в сборке.
    ///
    /// По умолчанию это <see cref="DiagnosticSeverity.Info"/>: отказ - штатное
    /// поведение, а не проблема. Проект, который на ускорение рассчитывает, включает
    /// строгий режим и получает те же отказы предупреждением или ошибкой:
    ///
    /// <code>
    /// &lt;PropertyGroup&gt;&lt;XmlSerDeCompatStrict&gt;error&lt;/XmlSerDeCompatStrict&gt;&lt;/PropertyGroup&gt;
    /// &lt;ItemGroup&gt;&lt;CompilerVisibleProperty Include="XmlSerDeCompatStrict" /&gt;&lt;/ItemGroup&gt;
    /// </code>
    /// </summary>
    public static class CompatDiagnostics
    {
        public const string StrictPropertyName = "build_property.XmlSerDeCompatStrict";

        public const string NotAcceleratedId = "XMLSERDE001";
        public const string GenerationFailedId = "XMLSERDE002";

        public static DiagnosticDescriptor NotAccelerated(DiagnosticSeverity severity)
        {
            return new DiagnosticDescriptor(
                id: NotAcceleratedId,
                title: "Type is not accelerated by XmlSerDe.Compat",
                messageFormat: "'{0}' falls back to System.Xml.Serialization.XmlSerializer: {1}",
                category: "XmlSerDe",
                severity,
                isEnabledByDefault: true,
                description: "The type stays fully functional; it is simply served by the BCL serializer instead of generated code."
                );
        }

        /// <summary>
        /// Продюсер отказал уже после обхода графа. Это не отказ по замыслу,
        /// а пробел в белом списке обходчика, и он всегда виден предупреждением.
        /// </summary>
        public static DiagnosticDescriptor GenerationFailed(DiagnosticSeverity severity)
        {
            return new DiagnosticDescriptor(
                id: GenerationFailedId,
                title: "XmlSerDe.Compat code generation was abandoned",
                messageFormat: "XmlSerDe.Compat generated nothing: {0}",
                category: "XmlSerDe",
                severity,
                isEnabledByDefault: true,
                description: "Every affected type falls back to System.Xml.Serialization.XmlSerializer."
                );
        }

        /// <summary>
        /// Значения свойства: пусто или <c>false</c> - обычный режим,
        /// <c>true</c>/<c>warning</c> - предупреждение, <c>error</c> - ошибка сборки.
        /// </summary>
        public static DiagnosticSeverity ParseStrict(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DiagnosticSeverity.Info;
            }

            switch (value!.Trim().ToLowerInvariant())
            {
                case "error":
                    return DiagnosticSeverity.Error;
                case "true":
                case "warning":
                    return DiagnosticSeverity.Warning;
                default:
                    return DiagnosticSeverity.Info;
            }
        }
    }
}
#endif
