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
    /// Корень с собственным именем. Нужен ровно затем, чтобы <c>CanDeserialize</c>
    /// проверялся не на типе, у которого имя элемента совпадает с именем типа:
    /// <see cref="System.Xml.Serialization.XmlSerializer"/> отзывается здесь на
    /// <c>&lt;purchase&gt;</c> и <b>не</b> отзывается на <c>&lt;CompatRenamedRoot&gt;</c>
    /// (проверено прогоном).
    /// </summary>
    [XmlRoot("purchase")]
    public class CompatRenamedRoot
    {
        public int Number { get; set; }
    }

    /// <summary>
    /// Тип, который нигде не назван в <c>new XmlSerializer(typeof(...))</c>:
    /// единственная его точка вызова - <c>XmlSerializer.FromTypes</c>. Если
    /// коллектор точек вызова её не видит, тип молча не ускорится.
    /// </summary>
    public class CompatFromTypesSubject
    {
        public int Number { get; set; }
    }

    /// <summary>
    /// То же самое, но точка вызова - фабрика
    /// (<c>new XmlSerializerFactory().CreateSerializer(typeof(...))</c>).
    /// </summary>
    public class CompatFactorySubject
    {
        public int Number { get; set; }
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

    /// <summary>
    /// Отказ третьей породы: дело не в типе члена, а в том, что член вообще
    /// не попал в наш отбор. <c>init</c>-свойство
    /// <see cref="System.Xml.Serialization.XmlSerializer"/> и пишет, и читает
    /// (проверено прогоном), а генератор его пропускает - присвоить <c>init</c>
    /// вне инициализатора объекта нельзя.
    ///
    /// Пока пропуск члена и отказ от типа были независимы, этот тип отчитывался
    /// ускоренным и терял <c>Y</c> в обе стороны молча.
    /// </summary>
    public class CompatInitSubject
    {
        public int Z { get; set; }
        public int Y { get; init; }
    }

    /// <summary>
    /// То же расхождение, но без всякого нового синтаксиса: свойство без сеттера
    /// типа <see cref="System.Collections.ObjectModel.Collection{T}"/> штатный
    /// сериализатор наполняет через <c>Add</c> (проверено прогоном), а генератор
    /// умеет так только <see cref="List{T}"/>.
    /// </summary>
    public class CompatFilledCollectionSubject
    {
        public int Z { get; set; }
        public System.Collections.ObjectModel.Collection<int> Items { get; } =
            new System.Collections.ObjectModel.Collection<int>();
    }

    /// <summary>
    /// Обратная сторона сверки составов: <c>internal</c>-член штатный сериализатор
    /// не видит (он берёт члены через <c>BindingFlags.Public</c> - проверено
    /// прогоном и на поле, и на свойстве), а мы выбрасывали только private
    /// и protected - и писали в документ лишний элемент.
    /// </summary>
    public class CompatInternalMemberSubject
    {
        public int Z { get; set; }
        internal int Hidden { get; set; }
    }

    /// <summary>
    /// Предохранитель: пропуски, которые <b>совпадают</b> с пропусками штатного
    /// сериализатора, отказом быть не должны, иначе ускорение потерял бы почти
    /// всякий настоящий POCO. Свойство без сеттера типа строки и
    /// <c>readonly</c>-поле не-коллекции пропускают оба, а <c>readonly</c>-поле
    /// типа <see cref="List{T}"/> оба наполняют через <c>Add</c>.
    /// </summary>
    public class CompatMatchingSkipSubject
    {
        public int Z { get; set; }
        public string ReadOnlyString { get; } = "skipped by both";
        public readonly int ReadOnlyNumber = 5;
        public readonly List<int> Filled = new List<int>();
    }
}
