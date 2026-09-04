#if NETSTANDARD
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    /// <summary>
    /// Диагностика о форме стока и инжектора.
    ///
    /// Генератор пишет вызов по конкретному типу, названному в
    /// <c>[XmlExhauster]</c> / <c>[XmlInjector]</c>. Если этот тип запечатан,
    /// JIT знает точный тип статически, девиртуализует вызов и инлайнит тело -
    /// virtual-метод обходится ровно в ту же цену, что невиртуальный. Если не
    /// запечатан, инлайн теряется.
    ///
    /// Цена измерена (docs/dispatch-cost.md): на настоящем стоке разница
    /// незапечатанного типа тонет в шуме, на вырожденном теле это впятеро, а
    /// полиморфный сайт стоит 1.2-1.8x. Поэтому это предупреждение, а не
    /// ошибка: код работает и так, просто медленнее, чем мог бы.
    /// </summary>
    public static class SinkDiagnostics
    {
        public const string NotSealedId = "XMLSERDE010";

        public static readonly DiagnosticDescriptor NotSealed =
            new DiagnosticDescriptor(
                id: NotSealedId,
                title: "XmlSerDe sink type is not sealed",
                messageFormat: "'{0}' is registered as a {1} but is not sealed: the generated call cannot be devirtualized. Seal it to get the performance of a non-virtual call.",
                category: "XmlSerDe",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "The generated code calls the sink by its concrete type. A sealed type lets the JIT devirtualize and inline; an open one does not. See docs/dispatch-cost.md for the measured cost."
                );
    }
}
#endif
