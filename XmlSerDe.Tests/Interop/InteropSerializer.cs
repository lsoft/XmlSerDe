//using System здесь обязателен, хотя в самом файле ничего из System не используется:
//генератор переносит в сгенерированный файл список using'ов того файла, где объявлен
//класс-сериализатор (см. ClassSourceProducer.GenerateUsings), а сгенерированному коду
//нужны System.MemoryExtensions и System.InvalidOperationException. Без этой строки
//сборка падает на сгенерированном файле, а не на пользовательском.
using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Interop.Subject;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Один класс-сериализатор на весь дифференциальный корпус.
    ///
    /// Объём этого списка - сам по себе результат замера: у BCL точка входа
    /// <c>new XmlSerializer(typeof(T))</c> и больше ничего, здесь же каждый тип графа
    /// приходится объявить вручную, а каждого наследника - ещё и вторым атрибутом,
    /// дублирующим уже стоящий на типе <see cref="System.Xml.Serialization.XmlIncludeAttribute"/>.
    /// Порядок атрибутов значим: XmlDerivedSubject ищет базу среди уже разобранных,
    /// поэтому база обязана стоять выше своих наследников.
    /// </summary>
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]

    //скаляры
    [XmlSubject(typeof(ScalarsSubject), true)]
    [XmlSubject(typeof(NullableSubject), true)]
    [XmlSubject(typeof(StringsSubject), true)]
    [XmlSubject(typeof(DateTimeKindsSubject), true)]
    [XmlSubject(typeof(FieldsSubject), true)]
    [XmlSubject(typeof(EmptySubject), true)]

    //коллекции и вложенность
    [XmlSubject(typeof(ChildSubject), false)]
    [XmlSubject(typeof(NestedSubject), true)]
    [XmlSubject(typeof(ListSubject), true)]
    [XmlSubject(typeof(ArraySubject), true)]
    [XmlSubject(typeof(EmptyCollectionsSubject), true)]
    [XmlSubject(typeof(GetOnlyCollectionSubject), true)]

    //наследование и полиморфизм
    [XmlSubject(typeof(InheritanceDerived), true)]
    [XmlSubject(typeof(PolyBase), false)]
    [XmlDerivedSubject(typeof(PolyBase), typeof(PolyDerived1))]
    [XmlDerivedSubject(typeof(PolyBase), typeof(PolyDerived2))]
    [XmlSubject(typeof(PolyDerived1), false)]
    [XmlSubject(typeof(PolyDerived2), false)]
    [XmlSubject(typeof(PolyHolder), true)]
    [XmlSubject(typeof(PolyListHolder), true)]
    [XmlSubject(typeof(ConcreteBase), false)]
    [XmlDerivedSubject(typeof(ConcreteBase), typeof(ConcreteDerived))]
    [XmlSubject(typeof(ConcreteDerived), false)]
    [XmlSubject(typeof(ConcreteBaseHolder), true)]

    //атрибутная модель System.Xml.Serialization
    [XmlSubject(typeof(EnumSubject), true)]
    [XmlSubject(typeof(RenamedEnumSubject), true)]
    [XmlSubject(typeof(IgnoreSubject), true)]
    [XmlSubject(typeof(RenamedElementSubject), true)]
    [XmlSubject(typeof(AttributeSubject), true)]
    [XmlSubject(typeof(RootRenamedSubject), true)]
    [XmlSubject(typeof(TypeRenamedSubject), true)]
    [XmlSubject(typeof(RenamedArraySubject), true)]
    [XmlSubject(typeof(TextSubject), true)]
    [XmlSubject(typeof(SpecifiedSubject), true)]
    [XmlSubject(typeof(DefaultValueSubject), true)]
    [XmlSubject(typeof(OrderedSubject), true)]
    public partial class InteropSerializer
    {
    }
}
