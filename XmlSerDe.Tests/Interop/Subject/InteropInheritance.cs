#nullable disable

using System.Collections.Generic;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Interop.Subject
{
    /// <summary>
    /// База с собственными членами. Нужна ровно затем, чтобы стало видно, в каком порядке
    /// пишутся члены при наследовании: BCL кладёт сначала базовые, XmlSerDe - сначала свои.
    /// В корпусе XmlSerDe.Tests такой формы не было (у BaseInfo только абстрактное
    /// get-свойство, которое не сериализуется), поэтому расхождение и не всплывало.
    /// </summary>
    public class InheritanceBase
    {
        public int BaseNumber { get; set; }
        public string BaseName { get; set; }
    }

    public class InheritanceDerived : InheritanceBase
    {
        public int DerivedNumber { get; set; }
        public string DerivedName { get; set; }
    }

    /// <summary>
    /// Абстрактная база с наследниками - канонический для обеих сторон полиморфизм
    /// через xsi:type. Наследники объявляются одним и тем же штатным
    /// <see cref="XmlIncludeAttribute"/>: своего атрибута для этого у XmlSerDe
    /// больше нет.
    /// </summary>
    [XmlInclude(typeof(PolyDerived1))]
    [XmlInclude(typeof(PolyDerived2))]
    public abstract class PolyBase
    {
        public int Common { get; set; }
    }

    public class PolyDerived1 : PolyBase
    {
        public string First { get; set; }
    }

    public class PolyDerived2 : PolyBase
    {
        public int Second { get; set; }
    }

    /// <summary>
    /// Полиморфный член.
    /// </summary>
    public class PolyHolder
    {
        public PolyBase Item { get; set; }
    }

    /// <summary>
    /// Полиморфизм внутри коллекции.
    /// </summary>
    public class PolyListHolder
    {
        public List<PolyBase> Items { get; set; }
    }

    /// <summary>
    /// Не-абстрактная база, у которой есть наследники, и экземпляр именно базы.
    /// Случай, на котором генератор строит цепочку "is Derived" без ветки на сам
    /// базовый тип.
    /// </summary>
    [XmlInclude(typeof(ConcreteDerived))]
    public class ConcreteBase
    {
        public int Common { get; set; }
    }

    public class ConcreteDerived : ConcreteBase
    {
        public string Extra { get; set; }
    }

    public class ConcreteBaseHolder
    {
        public ConcreteBase Item { get; set; }
    }
}
