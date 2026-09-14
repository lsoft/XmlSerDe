using System;

namespace XmlSerDe
{
    /// <summary>
    /// Opt-in XML 1.0 constructions that a native serializer understands.
    /// Default (<see cref="None"/>) is POCO data binding: the generator does not
    /// emit code for these. Combine values; <see cref="SystemXmlCompatible"/> is
    /// the set that <c>System.Xml.Serialization.XmlSerializer</c> accepts on the
    /// supported type subset, and the set the compat facade enables by itself.
    ///
    /// Флаг здесь заводится только под свою цену. Разделять то, что исполняется
    /// одним и тем же кодом, смысла нет: замер (README, "Cost of turning a flag
    /// on") показывает, что вход на разметочный путь стоит ~14% разбора, а какие
    /// именно конструкции на нём распознаются - уже почти ничего. Поэтому
    /// комментарии, PI и DOCTYPE - один <see cref="Markup"/>, а не три флага.
    /// </summary>
    [Flags]
    public enum XmlFeature
    {
        None = 0,

        /// <summary>
        /// Разметка между элементами и в прологе: комментарии
        /// <c>&lt;!-- ... --&gt;</c>, processing instructions <c>&lt;?...?&gt;</c>
        /// и <c>&lt;!DOCTYPE ...&gt;</c>. Без этого флага любая из них - ошибка
        /// разбора, а не пропуск.
        ///
        /// XML declaration к разметке не относится: её по-прежнему снимает
        /// только явный вызов <c>CutXmlHead</c>.
        /// </summary>
        Markup = 1 << 0,

        /// <summary>
        /// CDATA-секции: в тексте элемента разворачиваются в значение, между
        /// элементами пропускаются. Включает в себя <see cref="Markup"/> -
        /// признать <c>&lt;![CDATA[</c> и при этом споткнуться о комментарий
        /// значило бы платить за разметочный путь, не получая его.
        ///
        /// Единственная фича, меняющая не только сканер: у
        /// <see cref="IInjector"/> перегрузки для CDATA нет, поэтому строковые
        /// члены такого хоста декодируются напрямую, минуя инжектор
        /// (docs/opt-in-xml-features.md §16).
        /// </summary>
        CData = Markup | (1 << 1),

        /// <summary>
        /// The <c>xsi</c> prefix is not hard-coded: look for
        /// <c>xmlns:*=http://www.w3.org/2001/XMLSchema-instance</c> and read
        /// <c>type</c>/<c>nil</c> with that prefix. Without this flag the
        /// significant names are the literals <c>xsi:type</c> and <c>xsi:nil</c>.
        /// </summary>
        FlexibleXsiPrefix = 1 << 2,

        /// <summary>
        /// Before escaping a string on serialize — <see cref="XmlCharGuard"/>
        /// (illegal XML 1.0 §2.2 characters → <c>ArgumentException</c>).
        /// Without this flag an illegal character may appear in the output XML.
        /// </summary>
        CharGuard = 1 << 3,

        /// <summary>
        /// Enough for a native serializer to understand the same input
        /// constructions as <c>System.Xml.Serialization.XmlSerializer</c> on
        /// the supported type subset. The compat layer receives this value
        /// automatically.
        /// </summary>
        SystemXmlCompatible =
            CData
            | FlexibleXsiPrefix
            | CharGuard,
    }
}
