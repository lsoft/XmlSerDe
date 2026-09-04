using System;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Какие нарушения well-formedness хост обязан ловить. Несколько атрибутов
    /// на одном классе объединяются через OR; <see cref="XmlGuard.None"/> - то же
    /// самое, что отсутствие атрибута. Subject-типы, члены и сборки игнорируются:
    /// строгость - свойство читателя документа, а не отображаемого типа.
    ///
    /// Ставится рядом с <c>[XmlSubject]</c> и <c>[XmlFeatures]</c>; складывать
    /// стражи в <c>[XmlFeatures]</c> вторым аргументом нельзя - оси разные
    /// (docs/opt-in-xml-guards.md §4.2).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class XmlGuardsAttribute : Attribute
    {
        public XmlGuard Guards { get; }

        public XmlGuardsAttribute(XmlGuard guards)
        {
            Guards = guards;
        }
    }
}
