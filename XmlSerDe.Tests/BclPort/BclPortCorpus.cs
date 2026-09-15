#nullable disable

using System;
using System.Collections.Generic;
using XmlSerDe;
using XmlSerDe.Tests.Interop;
using XmlSerDe.Tests.BclPort.Subject;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// Корпус форм, перенесённых из тестового набора
    /// <c>System.Xml.Serialization</c>. Прогоняется тем же
    /// <see cref="InteropRunner"/>, что и наш собственный корпус: объект идёт
    /// через обе реализации во всех сочетаниях, и три направления - "читаем
    /// чужой вывод", "отдают наш на чтение", "совпадает сам формат" -
    /// проверяются порознь.
    ///
    /// Значения взяты из исходных тестов там, где они что-то значат
    /// (<c>Lily&amp;Lucy</c>, одна миллисекунда, 99 при выключенном
    /// <c>Specified</c>), и придуманы там, где первоисточник брал их из
    /// <c>Random</c>.
    /// </summary>
    public static class BclPortCorpus
    {
        private static string Write<T>(T obj, Action<StringBuilderExhauster, T> serialize)
        {
            var exhauster = new StringBuilderExhauster();
            serialize(exhauster, obj);
            return exhauster.ToString();
        }

        private static InteropResult Check<T>(
            T obj,
            Action<StringBuilderExhauster, T> serialize,
            XmlSerDeReader<T> deserialize
            )
        {
            return InteropRunner.Verify(
                obj,
                o => Write(o, serialize),
                deserialize
                );
        }

        #region скаляры и умолчания

        /// <summary>
        /// <c>Xml_SimpleType</c>.
        /// </summary>
        public static InteropResult SimpleType() => Check(
            new SimpleType { P1 = "foo", P2 = 1 },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out SimpleType r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithByteProperty</c>.
        /// </summary>
        public static InteropResult ByteProperty() => Check(
            new TypeWithByteProperty { ByteProperty = 123 },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithByteProperty r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithTimeSpanProperty</c>: одна миллисекунда - <c>PT0.001S</c>.
        /// </summary>
        public static InteropResult TimeSpanProperty() => Check(
            new TypeWithTimeSpanProperty { TimeSpanProperty = TimeSpan.FromMilliseconds(1) },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithTimeSpanProperty r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithFieldNameEndBySpecified</c>: один спутник включён,
        /// другой выключен, и 99 в документ попасть не должны.
        /// </summary>
        public static InteropResult PropertyNameSpecified() => Check(
            new TypeWithPropertyNameSpecified
            {
                MyField = "MyField",
                MyFieldSpecified = true,
                MyFieldIgnored = 99,
                MyFieldIgnoredSpecified = false,
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithPropertyNameSpecified r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_FieldBackedSpecifiedMember_SetOnDeserialize</c>.
        /// </summary>
        public static InteropResult FieldBackedSpecified() => Check(
            new TypeWithFieldBackedSpecifiedMember { Foo = "SomeValue", FooSpecified = true },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithFieldBackedSpecifiedMember r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithPropertiesHavingDefaultValue_DefaultValue</c>: все
        /// четыре члена равны своим умолчаниям, поэтому в документе остаётся
        /// только <c>CharProperty</c> - у BCL <c>char</c> с умолчанием всё равно
        /// пишется (109).
        /// </summary>
        public static InteropResult PropertiesAtDefaultValue() => Check(
            new TypeWithPropertiesHavingDefaultValue
            {
                StringProperty = "DefaultString",
                EmptyStringProperty = "",
                IntProperty = 11,
                CharProperty = 'm',
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithPropertiesHavingDefaultValue r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithStringPropertyWithDefaultValue_NonDefaultValue</c>:
        /// тот же тип, но все члены отличаются от умолчаний и обязаны быть
        /// записаны все.
        /// </summary>
        public static InteropResult PropertiesAtNonDefaultValue() => Check(
            new TypeWithPropertiesHavingDefaultValue
            {
                StringProperty = "NonDefaultValue",
                EmptyStringProperty = "NonEmpty",
                IntProperty = 12,
                CharProperty = 'n',
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithPropertiesHavingDefaultValue r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_DefaultValueAttributeSetToNaNTest</c>: умолчание, которое
        /// не равно самому себе, поэтому все четыре члена со значением 0
        /// обязаны быть записаны.
        /// </summary>
        public static InteropResult DefaultValueNaN() => Check(
            new DefaultValuesSetToNaN(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DefaultValuesSetToNaN r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_DefaultValueAttributeSetToPositiveInfinityTest</c>: значение 0
        /// умолчанию не равно, поэтому пишется.
        /// </summary>
        public static InteropResult DefaultValueInfinityNotMet() => Check(
            new DefaultValuesSetToPositiveInfinity(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DefaultValuesSetToPositiveInfinity r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>SerializeWithDefaultValueSetToPositiveInfinityTest</c>: то же
        /// умолчание, но теперь совпавшее, - все четыре члена обязаны пропасть.
        /// </summary>
        public static InteropResult DefaultValueInfinityMet() => Check(
            new DefaultValuesSetToPositiveInfinity
            {
                DoubleProp = double.PositiveInfinity,
                FloatProp = float.PositiveInfinity,
                DoubleField = double.PositiveInfinity,
                SingleField = float.PositiveInfinity,
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DefaultValuesSetToPositiveInfinity r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithEnumPropertyHavingDefaultValue</c>, первый прогон:
        /// <c>Option0</c> при <c>[DefaultValue(1)]</c> - значение умолчанию
        /// не равно и обязано быть записано.
        /// </summary>
        public static InteropResult EnumDefaultValueNotMet() => Check(
            new TypeWithEnumPropertyHavingDefaultValue { EnumProperty = IntEnum.Option0 },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithEnumPropertyHavingDefaultValue r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// Второй прогон того же теста: <c>Option1</c> - число 1 из атрибута
        /// обязано быть сопоставлено с членом перечисления, и член пропасть.
        /// Это и есть проверка, что несовпадение типов в атрибуте не превратилось
        /// в «умолчания нет вовсе».
        /// </summary>
        public static InteropResult EnumDefaultValueMet() => Check(
            new TypeWithEnumPropertyHavingDefaultValue { EnumProperty = IntEnum.Option1 },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithEnumPropertyHavingDefaultValue r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithMismatchBetweenAttributeAndPropertyType</c>:
        /// <c>[DefaultValue(true)]</c> при члене <c>int</c> - несопоставимо,
        /// значит атрибут пишется всегда.
        /// </summary>
        public static InteropResult MismatchedDefaultValue() => Check(
            new TypeWithMismatchBetweenAttributeAndPropertyType(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithMismatchBetweenAttributeAndPropertyType r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithShouldSerializeMethod_WithDefaultValue</c>: значение
        /// равно умолчанию, и <c>ShouldSerializeFoo()</c> обязан его скрыть.
        /// </summary>
        public static InteropResult ShouldSerializeAtDefault() => Check(
            new TypeWithShouldSerializeMethod(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithShouldSerializeMethod r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithShouldSerializeMethod_WithNonDefaultValue</c>: тот же
        /// тип с изменённым значением - оно обязано быть записано.
        /// </summary>
        public static InteropResult ShouldSerializeAtNonDefault() => Check(
            new TypeWithShouldSerializeMethod { Foo = "not default" },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithShouldSerializeMethod r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TestTypeWithPrivateOrNoSetters</c>, часть про свойство
        /// без сеттера.
        /// </summary>
        public static InteropResult NoSetters() => Check(
            new TypeWithNoSetters(25),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithNoSetters r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithSpecialCharacterInStringMember</c>.
        /// </summary>
        public static InteropResult SpecialCharacterInMember() => Check(
            new TypeA { Name = "Lily&Lucy" },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeA r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeNamesWithSpecialCharacters</c>.
        /// </summary>
        public static InteropResult SpecialCharactersInNames() => Check(
            new __TypeNameWithSpecialCharacters漢ñ { PropertyNameWithSpecialCharacters漢ñ = "Test" },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out __TypeNameWithSpecialCharacters漢ñ r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        #endregion

        #region коллекции

        /// <summary>
        /// <c>Xml_ArraylikeMembers</c>, заполненный прогон.
        /// </summary>
        public static InteropResult ArraylikeMembersPopulated() => Check(
            TypeWithArraylikeMembers.CreateWithPopulatedMembers(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithArraylikeMembers r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_ArraylikeMembers</c>, пустой прогон: все восемь членов -
        /// пустые элементы, и ни один не должен превратиться в null.
        /// </summary>
        public static InteropResult ArraylikeMembersEmpty() => Check(
            TypeWithArraylikeMembers.CreateWithEmptyMembers(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithArraylikeMembers r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_ArraylikeMembers</c>, null-прогон. Самый интересный: два члена
        /// с <c>[XmlArray(IsNullable = true)]</c> пишутся как
        /// <c>xsi:nil="true"</c>, остальные пропускаются, а на чтении массив
        /// остаётся null, тогда как <c>List&lt;T&gt;</c> становится пустым.
        /// </summary>
        public static InteropResult ArraylikeMembersNull() => Check(
            TypeWithArraylikeMembers.CreateWithNullMembers(),
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithArraylikeMembers r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_ArrayAsGetSet</c>, заполненный прогон.
        /// </summary>
        public static InteropResult GetSetArrayMembers() => Check(
            new TypeWithGetSetArrayMembers
            {
                F1 = new[] { new SimpleType { P1 = "ab", P2 = 1 }, new SimpleType { P1 = "cd", P2 = 2 } },
                F2 = new[] { -1, 3 },
                P1 = new[] { new SimpleType { P1 = "ef", P2 = 5 }, new SimpleType { P1 = "gh", P2 = 7 } },
                P2 = new[] { 11, 12 },
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithGetSetArrayMembers r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_ArrayAsGetSet</c>, второй прогон: null вперемешку с пустыми.
        /// </summary>
        public static InteropResult GetSetArrayMembersNullAndEmpty() => Check(
            new TypeWithGetSetArrayMembers
            {
                F1 = null,
                F2 = new int[] { },
                P1 = new SimpleType[] { },
                P2 = null,
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithGetSetArrayMembers r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// Выжимка из <c>Xml_PrimitiveArraysAndCollections</c>.
        /// </summary>
        public static InteropResult PrimitiveCollections() => Check(
            new PrimitiveCollections
            {
                Chars = new[] { 'A', 'Ω' },
                Integers = new List<int> { -1, 0, 42 },
                EmptyIntegers = new int[0],
                Enums = new[] { PrimitiveCollectionEnum.One, PrimitiveCollectionEnum.Two },
                NullableIntegers = new int?[] { 7, null, 9 },
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out PrimitiveCollections r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TestDeserializingUnknownNode</c>: многострочный текст с отступами
        /// внутри элемента, пустой элемент и перечисление.
        /// </summary>
        public static InteropResult SlideDeck() => Check(
            new SlideDeck
            {
                Slides = new List<SerializableSlide>
                {
                    new SerializableSlide
                    {
                        ImageName = "SecondAdventureImage",
                        ImagePath = "",
                        Description = new SpotlightDescription { IsDynamic = true, Value = "Available Now!\n      Episode 2" },
                        EventType = SlideEventType.LaunchSection,
                        EventData = "Adventures.Episode2.Details",
                    },
                },
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out SlideDeck r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithNestedPublicType</c>.
        /// </summary>
        public static InteropResult NestedPublicType() => Check(
            new NestedTypeHolder
            {
                Levels = new List<TypeWithNestedPublicType.LevelData>
                {
                    new TypeWithNestedPublicType.LevelData { Name = "Foo" },
                    new TypeWithNestedPublicType.LevelData { Name = "Bar" },
                },
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out NestedTypeHolder r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        #endregion

        #region атрибутная модель

        /// <summary>
        /// <c>XML_TypeWithFieldsOrdered</c>.
        /// </summary>
        public static InteropResult FieldsOrdered() => Check(
            new TypeWithFieldsOrdered
            {
                IntField1 = 1,
                IntField2 = 2,
                StringField1 = "foo1",
                StringField2 = "foo2",
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithFieldsOrdered r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithBinaryProperty</c>: <c>DataType = "hexBinary"</c>
        /// против <c>base64Binary</c> на одинаковых байтах.
        /// </summary>
        public static InteropResult BinaryDataType()
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes("The quick brown fox.");

            return Check(
                new TypeWithBinaryProperty
                {
                    BinaryHexContent = bytes,
                    Base64Content = bytes,
                },
                (e, o) => BclPortSerializer.Serialize(e, o, false),
                (ReadOnlySpan<char> xml, out TypeWithBinaryProperty r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));
        }

        /// <summary>
        /// <c>Xml_EnumFlags</c>: <c>One | Four</c> - значение, которому не
        /// соответствует ни один объявленный член.
        /// </summary>
        public static InteropResult EnumFlagsCombination() => Check(
            new EnumFlagsSubject { Value = EnumFlags.One | EnumFlags.Four },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out EnumFlagsSubject r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_EnumFlags</c>, но значение - ровно один объявленный член:
        /// на нём <c>[Flags]</c> ничего не меняет, и стороны обязаны совпасть.
        /// </summary>
        public static InteropResult EnumFlagsSingle() => Check(
            new EnumFlagsSubject { Value = EnumFlags.Two },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out EnumFlagsSubject r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_TypeWithDateTimePropertyAsXmlTime</c>: значение то же, что
        /// в первоисточнике, - 549269870000 тиков в UTC, то есть 15:15:26.9870000Z.
        /// </summary>
        public static InteropResult DateTimeAsXmlTime() => Check(
            new TypeWithDateTimePropertyAsXmlTime
            {
                Value = new DateTime(549269870000L, DateTimeKind.Utc),
            },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeWithDateTimePropertyAsXmlTime r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        #endregion

        #region наследование со спрятанными членами

        /// <summary>
        /// <c>Xml_HiddenDerivedFieldTest</c>: наследник через статический тип базы.
        /// </summary>
        public static InteropResult HiddenDerivedField() => Check(
            (BaseClass)new DerivedClass { value = "on derived" },
            (e, o) => BclPortSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out BaseClass r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        /// <summary>
        /// <c>Xml_BaseClassAndDerivedClassWithSameProperty</c>.
        /// </summary>
        public static InteropResult SamePropertyNameInDerived()
        {
            var subject = new DerivedClassWithSameProperty
            {
                DateTimeProperty = new DateTime(100),
                IntProperty = 5,
                StringProperty = "TestString",
                ListProperty = new List<string> { "one", "two", "three" },
            };

            return Check(
                subject,
                (e, o) => BclPortSerializer.Serialize(e, o, false),
                (ReadOnlySpan<char> xml, out DerivedClassWithSameProperty r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r));
        }

        #endregion

        public static IReadOnlyList<InteropCase> All => new[]
        {
            new InteropCase(nameof(SimpleType), SimpleType),
            new InteropCase(nameof(ByteProperty), ByteProperty),
            new InteropCase(nameof(TimeSpanProperty), TimeSpanProperty),
            new InteropCase(nameof(PropertyNameSpecified), PropertyNameSpecified),
            new InteropCase(nameof(FieldBackedSpecified), FieldBackedSpecified),
            new InteropCase(nameof(PropertiesAtDefaultValue), PropertiesAtDefaultValue),
            new InteropCase(nameof(PropertiesAtNonDefaultValue), PropertiesAtNonDefaultValue),
            new InteropCase(nameof(DefaultValueNaN), DefaultValueNaN),
            new InteropCase(nameof(DefaultValueInfinityNotMet), DefaultValueInfinityNotMet),
            new InteropCase(nameof(DefaultValueInfinityMet), DefaultValueInfinityMet),
            new InteropCase(nameof(EnumDefaultValueNotMet), EnumDefaultValueNotMet),
            new InteropCase(nameof(EnumDefaultValueMet), EnumDefaultValueMet),
            new InteropCase(nameof(MismatchedDefaultValue), MismatchedDefaultValue),
            new InteropCase(nameof(ShouldSerializeAtDefault), ShouldSerializeAtDefault),
            new InteropCase(nameof(ShouldSerializeAtNonDefault), ShouldSerializeAtNonDefault),
            new InteropCase(nameof(NoSetters), NoSetters),
            new InteropCase(nameof(SpecialCharacterInMember), SpecialCharacterInMember),
            new InteropCase(nameof(SpecialCharactersInNames), SpecialCharactersInNames),
            new InteropCase(nameof(ArraylikeMembersPopulated), ArraylikeMembersPopulated),
            new InteropCase(nameof(ArraylikeMembersEmpty), ArraylikeMembersEmpty),
            new InteropCase(nameof(ArraylikeMembersNull), ArraylikeMembersNull),
            new InteropCase(nameof(GetSetArrayMembers), GetSetArrayMembers),
            new InteropCase(nameof(GetSetArrayMembersNullAndEmpty), GetSetArrayMembersNullAndEmpty),
            new InteropCase(nameof(PrimitiveCollections), PrimitiveCollections),
            new InteropCase(nameof(SlideDeck), SlideDeck),
            new InteropCase(nameof(NestedPublicType), NestedPublicType),
            new InteropCase(nameof(FieldsOrdered), FieldsOrdered),
            new InteropCase(nameof(BinaryDataType), BinaryDataType),
            new InteropCase(nameof(EnumFlagsCombination), EnumFlagsCombination),
            new InteropCase(nameof(EnumFlagsSingle), EnumFlagsSingle),
            new InteropCase(nameof(DateTimeAsXmlTime), DateTimeAsXmlTime),
            new InteropCase(nameof(HiddenDerivedField), HiddenDerivedField),
            new InteropCase(nameof(SamePropertyNameInDerived), SamePropertyNameInDerived),
        };
    }
}
