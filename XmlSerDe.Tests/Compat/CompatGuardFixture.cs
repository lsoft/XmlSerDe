#nullable disable

using System;
using System.IO;
using System.Xml;
using Xunit;
using BclXmlSerializer = System.Xml.Serialization.XmlSerializer;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Фасад отвергает malformed ввод так же, как штатный сериализатор
    /// (docs/opt-in-xml-guards.md §7, §11.3). Пользователь при этом не пишет
    /// ни <c>[XmlGuards]</c>, ни <c>[XmlFeatures]</c>: полный набор фасад
    /// включает себе сам.
    ///
    /// Ожидание в каждом тесте снимается прогоном самого BCL, а не пишется
    /// литералом: расхождение должно ловиться в тот момент, когда меняется
    /// поведение любой из сторон.
    ///
    /// Единственное, что здесь сверяется по литералу, - <b>отсутствие</b>
    /// позиции в сообщении: у span-парсера строки и колонки нет, и это
    /// зафиксированное расхождение, а не случайность (§6).
    /// </summary>
    public class CompatGuardFixture
    {
        private const string WellFormed =
            "<CompatSubject><Number>42</Number><Name>compat</Name></CompatSubject>";

        /// <summary>
        /// Матрица §2: каждый случай - это документ, который ядро без стражей
        /// принимает, а BCL отвергает.
        /// </summary>
        public static TheoryData<string, string> Matrix()
        {
            return new TheoryData<string, string>
            {
                //§2.1 несовпавший закрывающий тег сложного типа
                { "чужой close", "<CompatSubject><Number>42</Number></Wrong>" },
                //§2.1 обрезанный сложный тип
                { "обрезанный корень", "<CompatSubject><Number>42</Number>" },
                //§2.2 второй корень
                { "второй корень", WellFormed + WellFormed },
                //§2.2 мусор после корня
                { "мусор после корня", WellFormed + "not-xml" },
                //§2.3 дубль атрибута
                { "дубль атрибута", "<CompatHolder><Child n=\"1\" n=\"2\"><Title>t</Title></Child></CompatHolder>" },
                //§2.4 битый синтаксис атрибута
                { "нет кавычек", "<CompatHolder><Child n=1><Title>t</Title></Child></CompatHolder>" },
                { "нет '='", "<CompatHolder><Child n \"1\"><Title>t</Title></Child></CompatHolder>" },
            };
        }

        [Theory]
        [MemberData(nameof(Matrix))]
        public void MalformedDocument_IsRejectedLikeSystemXml_Test(string kind, string xml)
        {
            var bcl = new BclXmlSerializer(BclType(xml));
            var expected = Assert.Throws<InvalidOperationException>(
                () => bcl.Deserialize(new StringReader(xml))
                );
            Assert.NotNull(expected.InnerException);

            var serializer = new XmlSerializer(BclType(xml));
            Assert.True(serializer.IsAccelerated, kind + ": тест обязан проверять быстрый путь");

            var thrown = Assert.Throws<InvalidOperationException>(
                () => serializer.Deserialize(xml.AsSpan())
                );

            //наружный тип и текст - как у BCL, но без координат. Текст самого
            //BCL здесь не сверяется: на .NET Framework он приходит из
            //локализованных ресурсов, а фасад всегда пишет английский литерал
            Assert.Equal("There is an error in the XML document.", thrown.Message);

            //цепочка та же: InvalidOperationException -> XmlException
            Assert.IsType<XmlException>(expected.InnerException);
            var inner = Assert.IsType<XmlException>(thrown.InnerException);

            Assert.DoesNotContain("Line ", inner.Message);
            Assert.DoesNotContain("position ", inner.Message);
            Assert.Equal(0, inner.LineNumber);
            Assert.Equal(0, inner.LinePosition);
        }

        /// <summary>
        /// Well-formed документ фасад по-прежнему читает быстрым путём -
        /// строгость не должна была ничего сломать.
        /// </summary>
        [Theory]
        [InlineData(WellFormed)]
        [InlineData(WellFormed + "  \r\n")]
        //после корня XML разрешает Misc, и BCL такой документ читает молча
        [InlineData(WellFormed + "<!-- bye -->")]
        [InlineData(WellFormed + "<?pi data?>")]
        [InlineData("<?xml version=\"1.0\"?><!-- hi -->" + WellFormed + "<!-- bye -->")]
        public void WellFormedDocument_StillRoundTrips_Test(string xml)
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated);

            //эталон: BCL этот же документ читает
            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var expected = (CompatSubject)bcl.Deserialize(new StringReader(xml));

            var actual = (CompatSubject)serializer.Deserialize(xml.AsSpan());

            Assert.Equal(expected.Number, actual.Number);
            Assert.Equal(expected.Name, actual.Name);
        }

        /// <summary>
        /// Изоляция: native-хост в той же сборке стражи не наследует. Тот же
        /// документ, который фасад отвергает, он по-прежнему принимает.
        /// </summary>
        [Fact]
        public void NativeHostInSameAssembly_StillAcceptsForeignClosingTag_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.Throws<InvalidOperationException>(
                () => serializer.Deserialize("<CompatSubject><Number>42</Number></Wrong>".AsSpan())
                );

            XmlSerializerDeserializer2.Deserialize(
                XmlSerDe.Components.Injector.DefaultInjector.Instance,
                "<XmlObject2><IntProperty>42</IntProperty></Wrong>".AsSpan(),
                out XmlObject2 result
                );

            Assert.Equal(42, result.IntProperty);
        }

        private static Type BclType(string xml)
        {
            return xml.StartsWith("<CompatHolder", StringComparison.Ordinal)
                ? typeof(CompatHolder)
                : typeof(CompatSubject);
        }
    }
}
