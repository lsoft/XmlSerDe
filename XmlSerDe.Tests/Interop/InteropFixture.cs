#nullable disable

using Xunit;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Прибивает текущее состояние совместимости с <see cref="System.Xml.Serialization.XmlSerializer"/>:
    /// на каждую форму - три независимых утверждения и причина, по которой оно именно такое.
    ///
    /// Тест, ожидающий "НЕТ", не узаконивает расхождение, а караулит его: как только
    /// расхождение будет закрыто, тест покраснеет и потребует переписать сюда новое
    /// положение дел. Иначе прогресс виден только в отчёте, который никто не читает,
    /// а регресс не виден вовсе.
    /// </summary>
    public class InteropFixture
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
        public void Fields_Test() => AssertInterop(
            InteropCorpus.Fields(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "публичные поля обе стороны сериализуют наравне со свойствами");

        [Fact]
        public void Empty_Test() => AssertInterop(
            InteropCorpus.Empty(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "<X></X> и <X /> - одно и то же для обеих сторон");

        [Fact]
        public void Nested_Test() => AssertInterop(
            InteropCorpus.Nested(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "вложенный сложный тип и null-член совпадают полностью");

        [Fact]
        public void Lists_Test() => AssertInterop(
            InteropCorpus.Lists(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "обёртка по имени члена, элементы по имени типа - как у BCL");

        [Fact]
        public void Arrays_Test() => AssertInterop(
            InteropCorpus.Arrays(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "массив пишется так же, как List<T>");

        [Fact]
        public void Enums_Test() => AssertInterop(
            InteropCorpus.Enums(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "имя члена перечисления совпадает с его именем в XML");

        [Fact]
        public void Ignore_Test() => AssertInterop(
            InteropCorpus.Ignore(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlIgnore - единственный атрибут BCL, который генератор уже читает");

        [Fact]
        public void Scalars_Test() => AssertInterop(
            InteropCorpus.Scalars(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "все примитивы, которые XmlSerDe считает встроенными, совпадают полностью");

        [Fact]
        public void Strings_Test() => AssertInterop(
            InteropCorpus.Strings(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "строка во всех состояниях, включая пустую (<Empty /> от BCL) и null");

        [Fact]
        public void Inheritance_Test() => AssertInterop(
            InteropCorpus.Inheritance(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "члены базы идут первыми - тот же порядок, что у BCL");

        [Fact]
        public void Polymorphic_Test() => AssertInterop(
            InteropCorpus.Polymorphic(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "xsi:type диспетчеризуется одинаково, порядок членов базы и наследника совпадает");

        [Fact]
        public void PolymorphicList_Test() => AssertInterop(
            InteropCorpus.PolymorphicList(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "то же, что и в Polymorphic_Test, но внутри коллекции");

        [Fact]
        public void ConcreteBaseInstance_Test() => AssertInterop(
            InteropCorpus.ConcreteBaseInstance(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "экземпляр не-абстрактной базы, у которой есть наследники, пишется как сама база");

        [Fact]
        public void RenamedElement_Test() => AssertInterop(
            InteropCorpus.RenamedElement(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlElement(\"имя\") задаёт имя элемента на обеих сторонах сразу");

        [Fact]
        public void RenamedEnum_Test() => AssertInterop(
            InteropCorpus.RenamedEnum(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlEnum(\"имя\") подставляется и в switch записи, и в цепочку сравнений чтения");

        [Fact]
        public void RenamedRoot_Test() => AssertInterop(
            InteropCorpus.RenamedRoot(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlRoot(\"имя\") действует только на корень, поэтому корню достаётся "
                + "отдельный метод записи, а чтение принимает оба имени");

        [Fact]
        public void RenamedType_Test() => AssertInterop(
            InteropCorpus.RenamedType(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlType(\"имя\") заменяет имя типа везде: и в корне, и в xsi:type");

        [Fact]
        public void RenamedArray_Test() => AssertInterop(
            InteropCorpus.RenamedArray(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlArray переименовывает обёртку, XmlArrayItem - элементы");

        [Fact]
        public void Ordered_Test() => AssertInterop(
            InteropCorpus.Ordered(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XmlElement.Order переставляет члены на записи; на чтении порядок "
                + "и раньше был не важен - разбор идёт по имени, а не по позиции");

        [Fact]
        public void DateTimeKinds_Test() => AssertInterop(
            InteropCorpus.DateTimeKinds(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "формат дат совпадает с XmlConvert.ToString(RoundtripKind) во всех четырёх "
                + "Kind'ах: незначащие нули дробной части не пишутся, а на ровной секунде "
                + "пропадает и сама точка");

        [Fact]
        public void Nullables_Test() => AssertInterop(
            InteropCorpus.Nullables(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "null у Nullable<T> обе стороны пишут пустым элементом с xsi:nil=\"true\"; "
                + "объявление xmlns:xsi у BCL стоит на корне, у XmlSerDe - на самом элементе, "
                + "но это одно и то же имя в одном и том же URI");

        #endregion

        #region молчаливая потеря данных

        [Fact]
        public void GetOnlyCollection_Test() => AssertInterop(
            InteropCorpus.GetOnlyCollection(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "коллекция без сеттера пропускается: BCL такой член наполняет через Add "
                + "у уже созданного экземпляра, XmlSerDe требует сеттер");

        [Fact]
        public void EmptyCollections_Test() => AssertInterop(
            InteropCorpus.EmptyCollections(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: true,
            because: "пустая коллекция от BCL (<X />) теперь читается как пустая, а не как null, "
                + "и формат совпадает. Обратное направление всё ещё расходится, но уже на стороне BCL: "
                + "прочитав наш <EmptyList></EmptyList>, он материализует пустым и соседний NullList, "
                + "которого в документе нет вовсе. Отдельная загадка, разбирать её - в следующий заход");

        [Fact]
        public void Specified_Test() => AssertInterop(
            InteropCorpus.Specified(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: true,
            because: "XxxSpecified=false убирает член из документа на записи, а встреченный "
                + "элемент взводит спутник на чтении");

        #endregion

        #region расхождение унаследовано от BCL, а не наше

        /// <summary>
        /// Единственная форма, где «НЕТ» в обоих направлениях чтения - не дефект
        /// XmlSerDe: <see cref="System.ComponentModel.DefaultValueAttribute"/> лишает
        /// документ значения, а на чтении BCL умолчание <b>не</b> восстанавливает.
        /// Форма документа при этом совпадает точь-в-точь.
        ///
        /// Чтобы это не приняли за нашу потерю, тест отдельно показывает, что и BCL
        /// не замыкает round-trip на собственном выводе.
        /// </summary>
        [Fact]
        public void DefaultValue_Test()
        {
            AssertInterop(
                InteropCorpus.DefaultValue(),
                canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: true,
                because: "член, равный DefaultValue, не пишет ни одна из сторон, а на чтении "
                    + "умолчание не восстанавливает тоже ни одна: 42 превращается в 0 у обеих");

            var original = new Subject.DefaultValueSubject { Value = 42, Other = 1 };
            var bclXml = InteropRunner.SystemXmlSerialize(original);
            var bclBack = InteropRunner.SystemXmlDeserialize<Subject.DefaultValueSubject>(bclXml);

            Assert.Equal(0, bclBack.Value);
        }

        #endregion

        #region атрибутная модель System.Xml.Serialization не поддержана

        [Fact]
        public void XmlAttributeMember_Test() => AssertInterop(
            InteropCorpus.XmlAttributeMember(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlAttribute не поддержан: член уходит в дочерний элемент, а не в атрибут головы");

        [Fact]
        public void XmlTextMember_Test() => AssertInterop(
            InteropCorpus.XmlTextMember(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlText не поддержан: член уходит в дочерний элемент, а не в текст самого элемента");

        #endregion
    }
}
