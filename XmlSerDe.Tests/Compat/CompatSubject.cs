#nullable disable

using System.Collections.Generic;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Тип, который фасад обслуживает быстрым путём. Никакого <c>[XmlSubject]</c>
    /// на нём нет и класса-сериализатора рядом тоже: всё, что видит генератор, -
    /// это <c>new XmlSerializer(typeof(CompatSubject))</c> в тестах.
    /// </summary>
    public class CompatSubject
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public List<int> Values { get; set; }
    }

    /// <summary>
    /// Граф глубже одного типа: обход обязан дойти до <see cref="CompatChild"/>
    /// и до наследников, объявленных <see cref="XmlIncludeAttribute"/>, - ни того,
    /// ни другого в точке вызова не названо.
    /// </summary>
    public class CompatHolder
    {
        public CompatChild Child { get; set; }
        public List<CompatChild> Children { get; set; }
        public CompatBase Polymorphic { get; set; }
    }

    public class CompatChild
    {
        public string Title { get; set; }

        [XmlAttribute("n")]
        public int Number { get; set; }
    }

    [XmlInclude(typeof(CompatDerived))]
    public class CompatBase
    {
        public int BaseNumber { get; set; }
    }

    public class CompatDerived : CompatBase
    {
        public string DerivedName { get; set; }
    }

    /// <summary>
    /// Структура: <see cref="System.Xml.Serialization.XmlSerializer"/> её умеет
    /// (проверено), а XmlSerDe - нет, потому что сложному типу генератор пишет
    /// <c>new T()</c> и присваивает члены по одному. Обходчик обязан отказаться
    /// от неё целиком, и тип от этого не ломается: он просто идёт штатным путём.
    /// </summary>
    public struct UnacceleratedStruct
    {
        public int Number { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Тот же отказ, но по другой причине: <c>HashSet&lt;T&gt;</c> BCL сериализует
    /// (проверено), а из обобщённых коллекций XmlSerDe знает только
    /// <c>List&lt;T&gt;</c>. Угадывать здесь нечего - это отказ.
    /// </summary>
    public class UnacceleratedCollectionSubject
    {
        public HashSet<int> Set { get; set; }
        public int After { get; set; }
    }
}
