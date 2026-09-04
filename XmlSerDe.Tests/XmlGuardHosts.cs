using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Тип, на котором видно все четыре стража сразу: атрибут (дубль имени),
    /// строковый член (запрещённый Char), сложный член и коллекция (несовпавший
    /// закрывающий тег - у каждого свой цикл в генераторе).
    /// </summary>
    public class GuardSubject
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        /// <summary>
        /// Строковый атрибут: у значения атрибута свой путь разбора, и
        /// <see cref="XmlGuard.IllegalChars"/> обязан покрыть оба.
        /// </summary>
        [XmlAttribute("tag")]
        public string? Tag { get; set; }

        public string? Title { get; set; }

        public GuardChild? Child { get; set; }

        public List<GuardChild>? Items { get; set; }
    }

    public class GuardChild
    {
        public string? Name { get; set; }
    }

    //Лестница стражей на одном и том же графе типов: между хостами отличается
    //ровно [XmlGuards], поэтому разница в поведении - это цена включённого
    //флага, а не другой формы документа. Тем же приёмом сделана
    //Complex/FeatureLadderHosts для XmlFeature.

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardDefaultHost
    {
    }

    [XmlGuards(XmlGuard.MatchingEndTags)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardMatchingEndTagsHost
    {
    }

    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardSingleRootHost
    {
    }

    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardUniqueAttributesHost
    {
    }

    [XmlGuards(XmlGuard.IllegalChars)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardIllegalCharsHost
    {
    }

    [XmlGuards(XmlGuard.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardFullHost
    {
    }

    /// <summary>
    /// Полный набор стражей на хосте, который вдобавок понимает разметку, -
    /// то же сочетание, что у compat. Отдельный хост нужен из-за хвоста
    /// документа: после корня XML разрешает комментарии и PI, и такой хост
    /// обязан их принять (docs/opt-in-xml-guards.md §5.3).
    /// </summary>
    [XmlGuards(XmlGuard.SystemXmlCompatible)]
    [XmlFeatures(XmlFeature.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardFullMarkupHost
    {
    }

    /// <summary>
    /// Два атрибута на одном классе объединяются через OR - то же правило, что
    /// у <c>[XmlFeatures]</c>.
    /// </summary>
    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardTwoAttributesHost
    {
    }

    /// <summary>
    /// <see cref="XmlGuard.None"/> обязан быть неотличим от отсутствия атрибута.
    /// </summary>
    [XmlGuards(XmlGuard.None)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(GuardChild), false)]
    [XmlSubject(typeof(GuardSubject), true)]
    public partial class GuardNoneHost
    {
    }
}
