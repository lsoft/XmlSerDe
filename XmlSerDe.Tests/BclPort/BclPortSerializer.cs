//using System здесь обязателен - см. комментарий в InteropSerializer.cs:
//генератор переносит в сгенерированный файл список using'ов того файла, где
//объявлен класс-сериализатор
using System;
using XmlSerDe;
using XmlSerDe.Tests.BclPort.Subject;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// Класс-сериализатор для форм, перенесённых из тестового набора
    /// <c>System.Xml.Serialization</c>. Отдельный от <c>InteropSerializer</c>
    /// намеренно: там корпус наш и собран под вопрос "что мы умеем", здесь -
    /// чужой и собран под вопрос "что умеет BCL". Смешивать их значило бы
    /// сломать метрику "полностью совместимо N из M" в <c>interop-report.md</c>.
    ///
    /// Набор фич тот же, что у <c>InteropSerializer</c>: без
    /// <see cref="XmlFeature.SystemXmlCompatible"/> часть перенесённых форм
    /// не читалась бы по причинам, к самим формам отношения не имеющим
    /// (CDATA в <see cref="ServerSettings"/>, например).
    /// </summary>
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlFeatures(XmlFeature.SystemXmlCompatible)]

    //скаляры, умолчания, спутники Specified
    [XmlSubject(typeof(SimpleType), true)]
    [XmlSubject(typeof(TypeWithByteProperty), true)]
    [XmlSubject(typeof(TypeWithTimeSpanProperty), true)]
    [XmlSubject(typeof(TypeWithPropertyNameSpecified), true)]
    [XmlSubject(typeof(TypeWithFieldBackedSpecifiedMember), true)]
    [XmlSubject(typeof(TypeWithPropertiesHavingDefaultValue), true)]
    [XmlSubject(typeof(DefaultValuesSetToNaN), true)]
    [XmlSubject(typeof(DefaultValuesSetToPositiveInfinity), true)]
    [XmlSubject(typeof(TypeWithEnumPropertyHavingDefaultValue), true)]
    [XmlSubject(typeof(TypeWithShouldSerializeMethod), true)]
    [XmlSubject(typeof(TypeWithNoSetters), true)]
    [XmlSubject(typeof(TypeA), true)]
    [XmlSubject(typeof(__TypeNameWithSpecialCharacters漢ñ), true)]
    [XmlSubject(typeof(StringWithNullCharSubject), true)]
    [XmlSubject(typeof(ServerSettings), true)]

    //коллекции
    [XmlSubject(typeof(TypeWithArraylikeMembers), true)]
    [XmlSubject(typeof(TypeWithGetSetArrayMembers), true)]
    [XmlSubject(typeof(PrimitiveCollections), true)]
    [XmlSubject(typeof(SerializableSlide), false)]
    [XmlSubject(typeof(SpotlightDescription), false)]
    [XmlSubject(typeof(SlideDeck), true)]
    [XmlSubject(typeof(TypeWithNestedPublicType.LevelData), false)]
    [XmlSubject(typeof(NestedTypeHolder), true)]

    //атрибутная модель
    [XmlSubject(typeof(TypeWithFieldsOrdered), true)]
    [XmlSubject(typeof(TypeWithBinaryProperty), true)]
    [XmlSubject(typeof(EnumFlagsSubject), true)]
    [XmlSubject(typeof(TypeWithDateTimePropertyAsXmlTime), true)]
    [XmlSubject(typeof(TypeWithMismatchBetweenAttributeAndPropertyType), true)]

    //наследование со спрятанными членами
    [XmlSubject(typeof(BaseClass), true)]
    [XmlSubject(typeof(DerivedClassWithSameProperty), true)]
    public partial class BclPortSerializer
    {
    }
}
