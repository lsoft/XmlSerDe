using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XmlSerDe.Common;

namespace XmlSerDe.Tests
{
    //формы POCO, найденные ревью 2026-09-10: у каждой есть своя фикстура
    //в ReviewShapeFixture, а эталон поведения - System.Xml.Serialization

    public class ArrayItemPoint
    {
        public int X { get; set; }
    }

    /// <summary>
    /// <see cref="XmlArrayItemAttribute"/> на коллекции сложного типа: элемент
    /// зовётся не по типу, а по атрибуту.
    /// </summary>
    public class ArrayItemHolder
    {
        [XmlArrayItem("item")]
        public List<ArrayItemPoint> Points { get; set; } = new();
    }

    public class EmptyItemsStrings
    {
        public List<string> Items { get; set; } = new();
    }

    public class EmptyItemsObjects
    {
        public List<ArrayItemPoint> Items { get; set; } = new();
    }

    /// <summary>
    /// Многоуровневый <see cref="XmlIncludeAttribute"/>: наследник объявляет
    /// уже своего наследника, и базе он виден транзитивно.
    /// </summary>
    [XmlInclude(typeof(IncMid))]
    public class IncBase
    {
        public int A { get; set; }
    }

    [XmlInclude(typeof(IncLeaf))]
    public class IncMid : IncBase
    {
        public int B { get; set; }
    }

    public class IncLeaf : IncMid
    {
        public int C { get; set; }
    }

    public class IncHolder
    {
        public IncBase? Item { get; set; }
    }

    /// <summary>
    /// Оба наследника объявлены на базе, причём родитель раньше ребёнка:
    /// порядок объявления не должен решать, какой xsi:type получит лист.
    /// </summary>
    [XmlInclude(typeof(FlatMid))]
    [XmlInclude(typeof(FlatLeaf))]
    public class FlatBase
    {
        public int A { get; set; }
    }

    public class FlatMid : FlatBase
    {
        public int B { get; set; }
    }

    public class FlatLeaf : FlatMid
    {
        public int C { get; set; }
    }

    public class FlatHolder
    {
        public FlatBase? Item { get; set; }
    }

    [XmlSubject(typeof(ArrayItemHolder), true)]
    [XmlSubject(typeof(ArrayItemPoint), false)]
    [XmlSubject(typeof(EmptyItemsStrings), true)]
    [XmlSubject(typeof(EmptyItemsObjects), true)]
    public partial class ReviewCollectionHost
    {
    }

    [XmlSubject(typeof(IncHolder), true)]
    [XmlSubject(typeof(IncBase), false)]
    [XmlSubject(typeof(FlatHolder), true)]
    [XmlSubject(typeof(FlatBase), false)]
    public partial class ReviewIncludeHost
    {
    }
}
