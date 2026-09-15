#nullable disable

using Xunit;
using XmlSerDe.Tests.Interop;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// Формы, перенесённые из тестового набора <c>System.Xml.Serialization</c>,
    /// под тем же дифференциальным прогоном, что и наш собственный корпус:
    /// на каждую форму три независимых утверждения и причина, по которой оно
    /// именно такое.
    ///
    /// Как и в <see cref="InteropFixture"/>, тест, ожидающий "НЕТ", не узаконивает
    /// расхождение, а караулит его: закрыли расхождение - тест краснеет и требует
    /// переписать сюда новое положение дел. Читать такой красный тест надо
    /// по тексту <c>because</c>, а не по имени.
    ///
    /// На 2026-09-15 полностью совместимо 19 форм из 27; остальные восемь
    /// перечислены ниже в разделе "расхождения", каждое со своей причиной.
    /// </summary>
    public class BclPortFixture
    {
        private static void AssertInterop(
            InteropResult result,
            bool canReadSystemXml,
            bool systemXmlCanReadOurs,
            bool sameShape,
            string because
            )
        {
            Assert.True(
                canReadSystemXml == result.CanReadSystemXml,
                $"XmlSerDe читает System.Xml: ожидалось {canReadSystemXml}. {because}\r\n{result.Describe()}"
                );
            Assert.True(
                systemXmlCanReadOurs == result.SystemXmlCanReadOurs,
                $"System.Xml читает XmlSerDe: ожидалось {systemXmlCanReadOurs}. {because}\r\n{result.Describe()}"
                );
            Assert.True(
                sameShape == result.SameShape,
                $"Совпадение формата: ожидалось {sameShape}. {because}\r\n{result.Describe()}"
                );
        }

        #region полная совместимость

        [Fact]
        public void SimpleType_Test() => AssertInterop(
            BclPortCorpus.SimpleType(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "нулевая отметка набора: строка и int свойствами");

        [Fact]
        public void ByteProperty_Test() => AssertInterop(
            BclPortCorpus.ByteProperty(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "byte пишется как unsignedByte у обеих сторон");

        /// <summary>
        /// <c>Xml_TypeWithTimeSpanProperty</c>. Одна из двух перенесённых форм,
        /// чей результат зависит от таргета, и там мы расходимся <b>в лучшую
        /// сторону</b>: поддержку <see cref="System.TimeSpan"/> в
        /// <c>XmlSerializer</c> завезли только в .NET Core, а на .NET Framework
        /// у типа нет ни одного публичного члена с сеттером, поэтому BCL пишет
        /// пустой элемент и теряет значение целиком. Ровно то же самое отдельно
        /// зафиксировано нашим <c>InteropFixture.Durations_Test</c>.
        /// </summary>
        [Fact]
        public void TimeSpanProperty_Test()
        {
#if NETFRAMEWORK
            AssertInterop(
                BclPortCorpus.TimeSpanProperty(),
                canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
                because: "на .NET Framework XmlSerializer не умеет TimeSpan и пишет пустой "
                    + "элемент; \"System.Xml читает XmlSerDe: да\" здесь не значит, что он "
                    + "что-то прочитал - он не видит этот член ни на записи, ни на чтении");

            //значение потеряно самим BCL, ещё до всякого чтения
            Assert.DoesNotContain(
                "PT0.001S",
                Interop.InteropRunner.SystemXmlSerialize(
                    new Subject.TypeWithTimeSpanProperty { TimeSpanProperty = System.TimeSpan.FromMilliseconds(1) }
                    )
                );
#else
            AssertInterop(
                BclPortCorpus.TimeSpanProperty(),
                canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
                because: "одна миллисекунда даёт PT0.001S у обеих сторон");
#endif
        }

        [Fact]
        public void PropertyNameSpecified_Test() => AssertInterop(
            BclPortCorpus.PropertyNameSpecified(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "выключенный спутник Specified прячет член целиком, и сами "
                + "спутники под [XmlIgnore] в документ не идут");

        [Fact]
        public void FieldBackedSpecified_Test() => AssertInterop(
            BclPortCorpus.FieldBackedSpecified(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "спутник полем работает так же, как спутник свойством");

        [Fact]
        public void PropertiesAtNonDefaultValue_Test() => AssertInterop(
            BclPortCorpus.PropertiesAtNonDefaultValue(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "значения отличаются от умолчаний, поэтому [DefaultValue] "
                + "ни на что не влияет и все четыре члена записаны");

        /// <summary>
        /// <c>Xml_TypeWithEnumPropertyHavingDefaultValue</c>, оба прогона
        /// исходного теста: <c>[DefaultValue(1)]</c> при члене-перечислении.
        /// Число из атрибута обязано быть сопоставлено со значением перечисления -
        /// <c>Option1</c> пропадает, <c>Option0</c> пишется.
        ///
        /// До правки генератора эта форма вообще не собиралась: сравнение
        /// <c>IntEnum != 1</c> даёт CS0019, и хост с ней ронял генерацию целиком.
        /// </summary>
        [Fact]
        public void EnumDefaultValueNotMet_Test() => AssertInterop(
            BclPortCorpus.EnumDefaultValueNotMet(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "Option0 умолчанию (1) не равен и обязан быть записан");

        [Fact]
        public void EnumDefaultValueMet_Test() => AssertInterop(
            BclPortCorpus.EnumDefaultValueMet(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "Option1 равен умолчанию, заданному числом 1, и обязан пропасть - "
                + "то есть число из атрибута приводится к перечислению, а не отбрасывается");

        /// <summary>
        /// <c>Xml_TypeWithMismatchBetweenAttributeAndPropertyType</c>:
        /// <c>[DefaultValue(true)]</c> при члене <c>int</c>. Сопоставить нечем,
        /// поэтому атрибут пишется всегда - у обеих сторон.
        ///
        /// Тоже не собиралось до правки (CS0019 на <c>int != true</c>).
        /// </summary>
        [Fact]
        public void MismatchedDefaultValue_Test() => AssertInterop(
            BclPortCorpus.MismatchedDefaultValue(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "умолчание несопоставимого типа не совпадает никогда, и член пишется всегда");

        [Fact]
        public void ShouldSerializeAtNonDefault_Test() => AssertInterop(
            BclPortCorpus.ShouldSerializeAtNonDefault(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "ShouldSerializeFoo() возвращает true, поэтому его непонимание "
                + "здесь ничего не меняет - расхождение видно только на умолчании");

        [Fact]
        public void NoSetters_Test() => AssertInterop(
            BclPortCorpus.NoSetters(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "свойство без сеттера обе стороны пропускают, несмотря на [XmlElement] на нём");

        [Fact]
        public void SpecialCharacterInMember_Test() => AssertInterop(
            BclPortCorpus.SpecialCharacterInMember(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "Lily&Lucy - амперсанд экранируется одинаково");

        [Fact]
        public void SpecialCharactersInNames_Test() => AssertInterop(
            BclPortCorpus.SpecialCharactersInNames(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "не-ASCII в именах типа и члена идут в имя элемента как есть");

        [Fact]
        public void ArraylikeMembersPopulated_Test() => AssertInterop(
            BclPortCorpus.ArraylikeMembersPopulated(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "восемь коллекций подряд - int[] и List<int>, полями и свойствами");

        [Fact]
        public void ArraylikeMembersEmpty_Test() => AssertInterop(
            BclPortCorpus.ArraylikeMembersEmpty(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "пустая коллекция - пустой элемент, и обратно она читается пустой, а не null");

        [Fact]
        public void GetSetArrayMembers_Test() => AssertInterop(
            BclPortCorpus.GetSetArrayMembers(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "массив сложного типа и массив int, полем и свойством");

        [Fact]
        public void GetSetArrayMembersNullAndEmpty_Test() => AssertInterop(
            BclPortCorpus.GetSetArrayMembersNullAndEmpty(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "null-массив пропускается и остаётся null на чтении, пустой пишется пустым элементом");

        [Fact]
        public void SlideDeck_Test() => AssertInterop(
            BclPortCorpus.SlideDeck(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "многострочный текст с отступами внутри элемента отдаётся как есть, "
                + "пустой элемент читается пустой строкой, а не null");

        [Fact]
        public void NestedPublicType_Test() => AssertInterop(
            BclPortCorpus.NestedPublicType(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "имя элемента вложенного типа - короткое, без объемлющего типа");

        [Fact]
        public void EnumFlagsSingle_Test() => AssertInterop(
            BclPortCorpus.EnumFlagsSingle(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "значение совпадает с объявленным членом, и [Flags] на него не влияет - "
                + "предохранитель к EnumFlagsCombination_Test, чтобы расхождение там "
                + "нельзя было списать на сам факт [Flags]");

        [Fact]
        public void HiddenDerivedField_Test() => AssertInterop(
            BclPortCorpus.HiddenDerivedField(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "наследник прячет члены базы через new, меняя свойство на поле и обратно; "
                + "в документ уходит член наследника, база остаётся пустой");

        [Fact]
        public void SamePropertyNameInDerived_Test() => AssertInterop(
            BclPortCorpus.SamePropertyNameInDerived(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "все четыре члена базы спрятаны одноимёнными членами наследника");

        #endregion

        #region расхождения

        /// <summary>
        /// Перенесено из <c>Xml_TypeWithPropertiesHavingDefaultValue_DefaultValue</c>,
        /// и эталонный XML первоисточника прямо это и показывает: у BCL из четырёх
        /// членов, равных своим умолчаниям, три пропущены, а <c>CharProperty</c>
        /// записан - <c>&lt;CharProperty&gt;109&lt;/CharProperty&gt;</c>.
        /// XmlSerDe сравнивает с умолчанием одинаково для всех типов и пропускает
        /// все четыре.
        ///
        /// Кто здесь прав - вопрос отдельный (поведение BCL похоже на его
        /// собственную особенность, а не на правило), но расхождение есть, и оно
        /// измерено. Читается документ при этом в обе стороны: теряется не
        /// значение, а только его наличие в тексте.
        /// </summary>
        [Fact]
        public void PropertiesAtDefaultValue_Test() => AssertInterop(
            BclPortCorpus.PropertiesAtDefaultValue(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "[DefaultValue('m')] на char BCL не применяет и пишет 109, а XmlSerDe "
                + "применяет и член пропускает");

        /// <summary>
        /// <c>ShouldSerializeXxx()</c> README среди поддержанного не называет,
        /// и здесь видно, чего именно это стоит: значение, равное умолчанию,
        /// BCL скрывает, а XmlSerDe пишет. Оба документа читаются обеими
        /// сторонами - расходится только текст.
        /// </summary>
        [Fact]
        public void ShouldSerializeAtDefault_Test() => AssertInterop(
            BclPortCorpus.ShouldSerializeAtDefault(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "ShouldSerializeFoo() XmlSerDe не читает, поэтому член со значением "
                + "по умолчанию всё равно попадает в документ");

        /// <summary>
        /// <c>[XmlArray(IsNullable = true)]</c>: у BCL null-коллекция пишется
        /// не пропуском элемента, а <c>&lt;X xsi:nil="true" /&gt;</c>.
        /// XmlSerDe <c>IsNullable</c> не читает и пропускает член, из-за чего
        /// его документ BCL прочитать одинаково уже не может: там, где ожидался
        /// nil, приходит отсутствие элемента, а <c>List&lt;T&gt;</c> от этого
        /// становится пустым списком вместо null.
        /// </summary>
        [Fact]
        public void ArraylikeMembersNull_Test() => AssertInterop(
            BclPortCorpus.ArraylikeMembersNull(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "[XmlArray(IsNullable = true)] XmlSerDe не читает: null пишется "
                + "пропуском элемента вместо xsi:nil=\"true\"");

        /// <summary>
        /// Пустой элемент внутри коллекции <c>Nullable&lt;int&gt;</c>: BCL пишет
        /// <c>&lt;int xsi:nil="true" /&gt;</c>, XmlSerDe - <c>&lt;int&gt;&lt;/int&gt;</c>.
        /// Читаем мы обе формы, а BCL на нашей падает: пустой <c>int</c> для него
        /// не число.
        ///
        /// Это та же дыра, что и в <see cref="ArraylikeMembersNull_Test"/>, но
        /// на элементе коллекции, а не на самой коллекции - закрываться они будут
        /// порознь, поэтому и караулятся порознь.
        /// </summary>
        [Fact]
        public void PrimitiveCollections_Test() => AssertInterop(
            BclPortCorpus.PrimitiveCollections(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "null-элемент коллекции Nullable<int> XmlSerDe пишет пустым элементом, "
                + "а не xsi:nil=\"true\", и прочитать такой документ BCL не может");

        /// <summary>
        /// Единственное расхождение набора, которое видно только на чтении:
        /// написанный нами документ BCL читает верно и форма совпадает, а вот
        /// чужой документ мы читаем неправильно.
        ///
        /// Причина - в постановке исходного теста: два члена носят одно и то же
        /// имя элемента <c>strfld</c>, и различить их можно только по позиции
        /// в последовательности <c>Order</c>. XmlSerDe связывает элемент с членом
        /// по имени, поэтому оба <c>strfld</c> попадают в один и тот же член,
        /// а второй остаётся пустым.
        /// </summary>
        [Fact]
        public void FieldsOrdered_Test() => AssertInterop(
            BclPortCorpus.FieldsOrdered(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: true,
            because: "[XmlElement(Order = n)] XmlSerDe применяет только на записи; "
                + "на чтении элемент связывается с членом по имени, и два одноимённых "
                + "strfld попадают в один член");

        /// <summary>
        /// <c>DataType =</c> README называет среди молча игнорируемых, и вот чего
        /// это стоит: <c>hexBinary</c> уходит в base64, документы не читаются
        /// ни в одну сторону, а прочитанное нами чужое значение оказывается
        /// вообще другими байтами - base64-декодирование шестнадцатеричного текста
        /// не падает, оно просто даёт мусор.
        ///
        /// Второй член, <c>base64Binary</c>, в той же форме совпадает: расходится
        /// именно неизвестный <c>DataType</c>, а не <c>byte[]</c> вообще.
        /// </summary>
        [Fact]
        public void BinaryDataType_Test() => AssertInterop(
            BclPortCorpus.BinaryDataType(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "[XmlElement(DataType = \"hexBinary\")] XmlSerDe не читает и пишет base64");

        /// <summary>
        /// <c>[Flags]</c>-комбинация: BCL пишет список имён через пробел
        /// (<c>One Four</c>) - это XSD-шный list, - а запасная ветка XmlSerDe
        /// уходит в <c>Enum.ToString()</c> и даёт <c>One, Four</c>. Ни одна
        /// сторона документ другой не читает: у нас <c>Enum.Parse</c> не знает
        /// пробела, у BCL - запятой.
        /// </summary>
        [Fact]
        public void EnumFlagsCombination_Test() => AssertInterop(
            BclPortCorpus.EnumFlagsCombination(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "[Flags]-комбинация у BCL - имена через пробел, у нас - через запятую "
                + "(Enum.ToString/Enum.Parse запасной ветки)");

        /// <summary>
        /// Расхождение, найденное этими двумя формами и не имеющее к
        /// <c>[DefaultValue]</c> никакого отношения: <b>порядок членов внутри
        /// одного типа</b>. BCL пишет сначала все поля, потом все свойства
        /// (в объявленном порядке внутри каждой группы), XmlSerDe - в порядке
        /// объявления, как они стоят в исходнике.
        ///
        /// Раньше это не показывалось ни одной формой корпуса: в
        /// <c>FieldsSubject</c> поля и так объявлены раньше свойств, и порядки
        /// совпадали случайно. Здесь свойства объявлены первыми, и разница видна.
        ///
        /// Выравнивать это - правка ядра, которая меняет документ у всех
        /// существующих потребителей, поэтому она согласовывается отдельно,
        /// а не делается заодно. Про порядок при наследовании (база первой)
        /// такая правка уже была и записана в docs/xmlserializer-compat.md.
        ///
        /// Значения при этом совпадают, и документы читаются в обе стороны:
        /// <c>NaN</c> не равен сам себе, поэтому все четыре члена пишутся у обеих
        /// сторон, - что и требовалось проверить исходным тестом.
        /// </summary>
        [Fact]
        public void DefaultValueNaN_Test() => AssertInterop(
            BclPortCorpus.DefaultValueNaN(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "значения и состав совпали; расходится порядок - BCL пишет поля "
                + "перед свойствами, XmlSerDe идёт по порядку объявления");

        [Fact]
        public void DefaultValueInfinityNotMet_Test() => AssertInterop(
            BclPortCorpus.DefaultValueInfinityNotMet(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "то же самое расхождение порядка, что и в DefaultValueNaN_Test: "
                + "0 не равен бесконечности, поэтому пишут обе стороны, но в разном порядке");

        /// <summary>
        /// <c>SerializeWithDefaultValueSetToPositiveInfinityTest</c>: умолчание
        /// совпало, и обе стороны пишут пустой элемент - формат сходится.
        ///
        /// А вот оба направления чтения показывают "НЕТ", и это **не наша потеря**:
        /// <c>[DefaultValue]</c> работает только на запись, на чтении его не
        /// восстанавливает никто, включая сам BCL. Поэтому объект после
        /// round-trip отличается от исходного у обеих сторон одинаково -
        /// что и проверяется прямым прогоном BCL ниже, до всякого XmlSerDe.
        /// </summary>
        [Fact]
        public void DefaultValueInfinityMet_Test()
        {
            AssertInterop(
                BclPortCorpus.DefaultValueInfinityMet(),
                canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: true,
                because: "умолчание совпало, документ у обеих сторон пустой; "
                    + "\"НЕТ\" в обоих направлениях - это потеря значения самим "
                    + "[DefaultValue], а не расхождение реализаций");

            //та же потеря у BCL наедине с собой: записал, прочитал, получил 0
            var subject = new Subject.DefaultValuesSetToPositiveInfinity
            {
                DoubleProp = double.PositiveInfinity,
                FloatProp = float.PositiveInfinity,
                DoubleField = double.PositiveInfinity,
                SingleField = float.PositiveInfinity,
            };

            var bclBack = InteropRunner.SystemXmlDeserialize<Subject.DefaultValuesSetToPositiveInfinity>(
                InteropRunner.SystemXmlSerialize(subject)
                );

            Assert.Equal(0d, bclBack.DoubleProp);
            Assert.Equal(0d, bclBack.DoubleField);
        }

        /// <summary>
        /// <c>[XmlText(DataType = "time")]</c>: BCL пишет только время суток
        /// и дату теряет, XmlSerDe <c>DataType</c> не читает и пишет полный
        /// <c>dateTime</c>. Чужой документ мы читаем (время без даты
        /// <c>DateTime.Parse</c> принимает), свой BCL - нет.
        /// </summary>
        [Fact]
        public void DateTimeAsXmlTime_Test() => AssertInterop(
            BclPortCorpus.DateTimeAsXmlTime(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "[XmlText(DataType = \"time\")] XmlSerDe не читает и пишет полный dateTime");

        #endregion
    }
}
