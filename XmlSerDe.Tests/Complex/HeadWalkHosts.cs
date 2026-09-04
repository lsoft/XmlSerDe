using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests.Complex
{
    /// <summary>
    /// Форма, ради которой затевался замер обходов головы (probe
    /// <c>--attr-cost</c>): голова, по которой сегодня ходят несколько
    /// независимых проходов. На одну такую ноду генератор emit-ит
    /// <c>ReadHead</c> (свой скан до неэкранированного '&gt;'), затем
    /// <c>GetXsiType()</c> (обход атрибутов - на этом типе всегда
    /// безрезультатный, но полный), затем по одному <c>ParseAttribute</c>
    /// на каждый <c>[XmlAttribute]</c>-член, каждый со своего начала.
    ///
    /// У REGULAR такой формы нет: там атрибуты стоят на трёх головах из
    /// двадцати шести, а <c>[XmlAttribute]</c>-членов нет вовсе, и проходов
    /// поэтому два, а не пять.
    /// </summary>
    public class AttrNode
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("tag")]
        public string? Tag { get; set; }

        [XmlAttribute("kind")]
        public string? Kind { get; set; }

        public string? Title { get; set; }
    }

    public class AttrContainer
    {
        public List<AttrNode>? Items { get; set; }
    }

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(AttrNode), false)]
    [XmlSubject(typeof(AttrContainer), true)]
    public partial class HeadWalkHost
    {
    }

    /// <summary>
    /// Тот же тип со стражем уникальности: от <see cref="HeadWalkHost"/>
    /// отличается ровно <c>[XmlGuards]</c>, поэтому разница в замере - это
    /// цена ещё одного полного обхода той же головы
    /// (<c>XmlScan.EnsureUniqueAttributes</c>) поверх тех, что уже есть.
    /// </summary>
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlSubject(typeof(AttrNode), false)]
    [XmlSubject(typeof(AttrContainer), true)]
    public partial class HeadWalkUniqueHost
    {
    }
}
