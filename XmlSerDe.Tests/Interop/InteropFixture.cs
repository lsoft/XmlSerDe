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

        #endregion

        #region формат совпадает, но обмен ломается

        [Fact]
        public void Scalars_Test() => AssertInterop(
            InteropCorpus.Scalars(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: true,
            because: "все примитивы совпадают, кроме DateTime: DefaultInjector зовёт DateTime.Parse "
                + "без DateTimeStyles.RoundtripKind, поэтому 'Z' на чтении превращается в локальное "
                + "время и Kind теряется");

        [Fact]
        public void Strings_Test() => AssertInterop(
            InteropCorpus.Strings(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: true,
            because: "пустую строку BCL пишет как <Empty />, а разбор считает узел без тела "
                + "отсутствующим значением и не присваивает член вовсе - пустая строка читается как null");

        [Fact]
        public void DateTimeKinds_Test() => AssertInterop(
            InteropCorpus.DateTimeKinds(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: false,
            because: "Kind теряется на чтении (см. Scalars_Test), а на записи дробная часть всегда "
                + "семь знаков: BCL для целой секунды пишет '...T14:30:45Z', XmlSerDe - '...T14:30:45.0000000Z'");

        [Fact]
        public void Nullables_Test() => AssertInterop(
            InteropCorpus.Nullables(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: false,
            because: "null-члена XmlSerDe не пишет вовсе, а BCL пишет <X xsi:nil=\"true\" />; "
                + "на чтении обе формы дают null, так что обмен от этого не страдает - страдает только формат. "
                + "Сам обмен ломает всё тот же DateTime.Kind");

        #endregion

        #region порядок членов

        [Fact]
        public void Inheritance_Test() => AssertInterop(
            InteropCorpus.Inheritance(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "BCL пишет сначала члены базы, XmlSerDe - сначала свои "
                + "(ClassSourceProducer.GetMembersOrderByInheritance идёт от типа к базе). "
                + "Обмен не страдает: обе стороны разбирают детей по имени, а не по позиции");

        [Fact]
        public void Polymorphic_Test() => AssertInterop(
            InteropCorpus.Polymorphic(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "xsi:type диспетчеризуется одинаково, расходится только порядок членов базы и наследника");

        [Fact]
        public void PolymorphicList_Test() => AssertInterop(
            InteropCorpus.PolymorphicList(),
            canReadSystemXml: true, systemXmlCanReadOurs: true, sameShape: false,
            because: "то же, что и в Polymorphic_Test, но внутри коллекции");

        [Fact]
        public void Ordered_Test() => AssertInterop(
            InteropCorpus.Ordered(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlElement.Order не читается, порядок остаётся объявленным в C#; "
                + "BCL же на чтении своего же порядка ждёт строго и второй член теряет");

        #endregion

        #region молчаливая потеря данных

        [Fact]
        public void ConcreteBaseInstance_Test() => AssertInterop(
            InteropCorpus.ConcreteBaseInstance(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "у не-абстрактной базы с наследниками генератор строит только цепочку "
                + "'is Derived' и не оставляет ветки на саму базу, поэтому экземпляр базы "
                + "сериализуется в пустоту - без ошибки и без предупреждения");

        [Fact]
        public void GetOnlyCollection_Test() => AssertInterop(
            InteropCorpus.GetOnlyCollection(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "коллекция без сеттера пропускается: BCL такой член наполняет через Add "
                + "у уже созданного экземпляра, XmlSerDe требует сеттер");

        [Fact]
        public void EmptyCollections_Test() => AssertInterop(
            InteropCorpus.EmptyCollections(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: true,
            because: "пустая коллекция от BCL приходит как <X /> и читается как null: "
                + "то же самое место, что и с пустой строкой - узел без тела не присваивает член");

        [Fact]
        public void Specified_Test() => AssertInterop(
            InteropCorpus.Specified(),
            canReadSystemXml: true, systemXmlCanReadOurs: false, sameShape: false,
            because: "паттерн XxxSpecified не поддержан: член пишется всегда, тогда как BCL "
                + "при ValueSpecified=false его опускает");

        [Fact]
        public void DefaultValue_Test() => AssertInterop(
            InteropCorpus.DefaultValue(),
            canReadSystemXml: false, systemXmlCanReadOurs: true, sameShape: false,
            because: "DefaultValue не поддержан: BCL опускает член, равный умолчанию, "
                + "и на чтении такого документа XmlSerDe оставляет 0 вместо 42");

        #endregion

        #region атрибутная модель System.Xml.Serialization не поддержана

        [Fact]
        public void RenamedElement_Test() => AssertInterop(
            InteropCorpus.RenamedElement(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlElement(\"имя\") не читается: элемент называется по члену C#");

        [Fact]
        public void RenamedEnum_Test() => AssertInterop(
            InteropCorpus.RenamedEnum(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlEnum(\"имя\") не читается: член перечисления пишется своим именем C#");

        [Fact]
        public void RenamedRoot_Test() => AssertInterop(
            InteropCorpus.RenamedRoot(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlRoot(\"имя\") не читается: корень называется по типу C#");

        [Fact]
        public void RenamedType_Test() => AssertInterop(
            InteropCorpus.RenamedType(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlType(\"имя\") не читается: и корень, и xsi:type называются по типу C#");

        [Fact]
        public void RenamedArray_Test() => AssertInterop(
            InteropCorpus.RenamedArray(),
            canReadSystemXml: false, systemXmlCanReadOurs: false, sameShape: false,
            because: "XmlArray/XmlArrayItem не читаются: обёртка называется по члену, элемент - по типу");

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
