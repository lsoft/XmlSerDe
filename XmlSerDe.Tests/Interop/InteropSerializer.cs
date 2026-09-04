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
    /// приходится объявить вручную.
    ///
    /// Наследников это больше не касается: генератор читает штатный
    /// <see cref="System.Xml.Serialization.XmlIncludeAttribute"/>, поэтому отдельного
    /// XmlSubject на самого наследника не нужно. Порядок атрибутов здесь тоже больше
    /// не значим - раньше его требовал XmlDerivedSubject, искавший базу среди уже
    /// разобранных.
    /// </summary>
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlFeatures(XmlFeature.SystemXmlCompatible)]

    //скаляры
    [XmlSubject(typeof(ScalarsSubject), true)]
    [XmlSubject(typeof(TrickyScalarsSubject), true)]
    [XmlSubject(typeof(DurationSubject), true)]
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

    //byte[] - коллекция, которая пишется не коллекцией
    [XmlSubject(typeof(BinarySubject), true)]
    [XmlSubject(typeof(BinaryAttributeSubject), true)]
    [XmlSubject(typeof(BinaryTextSubject), true)]
    [XmlSubject(typeof(BinaryArraySubject), true)]
    [XmlSubject(typeof(ByteListSubject), true)]

    //наследование и полиморфизм.
    //Наследники здесь не объявлены вовсе: и PolyBase, и ConcreteBase несут штатный
    //XmlInclude, а генератор читает его сам - вместе с регистрацией самих наследников
    [XmlSubject(typeof(InheritanceDerived), true)]
    [XmlSubject(typeof(PolyBase), false)]
    [XmlSubject(typeof(PolyHolder), true)]
    [XmlSubject(typeof(PolyListHolder), true)]
    [XmlSubject(typeof(ConcreteBase), false)]
    [XmlSubject(typeof(ConcreteBaseHolder), true)]

    //атрибутная модель System.Xml.Serialization
    [XmlSubject(typeof(EnumSubject), true)]
    [XmlSubject(typeof(RenamedEnumSubject), true)]
    [XmlSubject(typeof(IgnoreSubject), true)]
    [XmlSubject(typeof(RenamedElementSubject), true)]
    [XmlSubject(typeof(AttributeSubject), true)]
    [XmlSubject(typeof(AttributeTabSubject), true)]
    [XmlSubject(typeof(AttributeBase), false)]
    [XmlSubject(typeof(AttributeHolder), true)]
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
