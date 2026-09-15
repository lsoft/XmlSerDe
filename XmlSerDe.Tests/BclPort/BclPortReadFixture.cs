#nullable disable

using System;
using Xunit;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.BclPort.Subject;
using XmlSerDe.Tests.Interop;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// Перенесённые тесты, у которых вход - готовый документ, а не объект:
    /// круговой прогон их не выражает, потому что проверяется именно чтение
    /// текста, который ни одна из сторон не писала.
    ///
    /// Эталоном служит тот же живой <see cref="System.Xml.Serialization.XmlSerializer"/>:
    /// оба читают один и тот же документ, и результаты сравниваются по
    /// BCL-сериализации получившихся объектов - так же, как в
    /// <see cref="InteropRunner"/>, и по той же причине (писать <c>Equals</c>
    /// на каждую форму пришлось бы вручную).
    /// </summary>
    public class BclPortReadFixture
    {
        /// <summary>
        /// Читает один и тот же документ обеими сторонами и требует, чтобы
        /// получились одинаковые объекты.
        /// </summary>
        private static void AssertSameRead<T>(string xml, XmlSerDeReader<T> read, string because)
        {
            var theirs = InteropRunner.SystemXmlDeserialize<T>(xml);
            read(BuiltinCodeHelper.CutXmlHead(xml.AsSpan()), out var ours);

            var expected = InteropRunner.SystemXmlSerialize(theirs);
            var actual = InteropRunner.SystemXmlSerialize(ours);

            Assert.True(
                expected == actual,
                $"{because}\r\nдокумент: {xml}\r\nSystem.Xml прочитал: {expected}\r\nXmlSerDe прочитал: {actual}"
                );
        }

        /// <summary>
        /// <c>Xml_TestIgnoreWhitespaceForDeserialization</c>. Значения приходят
        /// в CDATA: у <c>DS2Root</c> секция окружена переводами строк и отступами,
        /// у <c>MetricConfigUrl</c> стоит вплотную к тегам.
        ///
        /// Расхождение, найденное переносом: пробелы <b>внутри</b> секции обе
        /// стороны сохраняют (<c>MetricConfigUrl</c> совпадает), а вот отступы
        /// <b>вокруг</b> неё BCL выбрасывает, XmlSerDe - оставляет. Причина
        /// у BCL внятная: соседний с CDATA текстовый узел из одних пробелов -
        /// незначащий whitespace, и <c>XmlTextReader</c> его не отдаёт вовсе;
        /// XmlSerDe же берёт тело элемента одним куском и раскрывает секцию
        /// на месте, не разделяя тело на узлы.
        ///
        /// Тест караулит расхождение, а не узаконивает его: закроют - покраснеет
        /// и потребует переписать сюда новое положение дел.
        /// </summary>
        [Fact]
        public void IgnoreWhitespaceAroundCData_Test()
        {
            const string Xml =
                "<?xml version=\"1.0\"?><ServerSettings>\r\n"
                + "  <DS2Root>\r\n"
                + "    <![CDATA[ http://wxdata.weather.com/wxdata/]]>\r\n"
                + "  </DS2Root>\r\n"
                + "  <MetricConfigUrl><![CDATA[ http://s3.amazonaws.com/windows-prod-twc/desktop8/beacons.xml ]]></MetricConfigUrl>\r\n"
                + "</ServerSettings>";

            var theirs = InteropRunner.SystemXmlDeserialize<ServerSettings>(Xml);
            BclPortSerializer.Deserialize(
                DefaultInjector.Instance,
                BuiltinCodeHelper.CutXmlHead(Xml.AsSpan()),
                out ServerSettings ours
                );

            //секция вплотную к тегам: пробелы внутри неё значащие, и они совпадают
            Assert.Equal(
                " http://s3.amazonaws.com/windows-prod-twc/desktop8/beacons.xml ",
                theirs.MetricConfigUrl
                );
            Assert.Equal(theirs.MetricConfigUrl, ours.MetricConfigUrl);

            //секция в отступах: BCL отдаёт только её содержимое...
            Assert.Equal(" http://wxdata.weather.com/wxdata/", theirs.DS2Root);

            //...а XmlSerDe - вместе с окружающими переводами строк и отступами.
            //CRLF документа при этом уже нормализован в LF по XML 1.0 §2.11,
            //и это как раз совпадающая часть поведения
            Assert.Equal(
                "\n    " + " http://wxdata.weather.com/wxdata/" + "\n  ",
                ours.DS2Root
                );
        }

        /// <summary>
        /// <c>Xml_DeserializeTypeWithEmptyTimeSpanProperty</c>: пустой элемент
        /// на месте <c>TimeSpan</c> - это <c>default</c>, а не ошибка разбора.
        /// </summary>
        [Fact]
        public void EmptyTimeSpanElement_Test()
        {
            const string Xml =
                "<?xml version=\"1.0\"?><TypeWithTimeSpanProperty><TimeSpanProperty /></TypeWithTimeSpanProperty>";

            AssertSameRead<TypeWithTimeSpanProperty>(
                Xml,
                (ReadOnlySpan<char> xml, out TypeWithTimeSpanProperty r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r),
                because: "<X /> на месте duration читается как TimeSpan.Zero");
        }

        /// <summary>
        /// <c>Xml_TestTypeWithPrivateOrNoSetters</c>, часть про свойство без
        /// сеттера: записанное значение обязано быть проигнорировано, а в объекте
        /// остаться то, что поставил конструктор, - 200.
        /// </summary>
        [Fact]
        public void NoSetterIsNotAssigned_Test()
        {
            const string Xml =
                "<?xml version=\"1.0\"?><TypeWithNoSetters><NoSetter>25</NoSetter></TypeWithNoSetters>";

            AssertSameRead<TypeWithNoSetters>(
                Xml,
                (ReadOnlySpan<char> xml, out TypeWithNoSetters r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r),
                because: "свойству без сеттера присвоить нечем, и обе стороны оставляют 200 от конструктора");
        }

        /// <summary>
        /// <c>Xml_TestDeserializingUnknownNode</c>: элемент, которому не
        /// соответствует ни один член, пропускается вместе со своим поддеревом,
        /// а разбор продолжается с следующего известного.
        /// </summary>
        [Fact]
        public void UnknownElementIsSkipped_Test()
        {
            const string Xml =
                "<?xml version=\"1.0\"?><SlideDeck><Slides>"
                + "<SerializableSlide>"
                + "<ImageName>SecondAdventureImage</ImageName>"
                + "<Unknown><Deeper attr=\"1\">text</Deeper></Unknown>"
                + "<EventType>LaunchSection</EventType>"
                + "<EventData>Adventures.Episode2.Details</EventData>"
                + "</SerializableSlide>"
                + "</Slides></SlideDeck>";

            AssertSameRead<SlideDeck>(
                Xml,
                (ReadOnlySpan<char> xml, out SlideDeck r) => BclPortSerializer.Deserialize(DefaultInjector.Instance, xml, out r),
                because: "неизвестный элемент пропускается вместе с поддеревом, а не обрывает разбор");
        }

        /// <summary>
        /// <c>Xml_DeserializeOutOfRangeByteProperty</c>: <c>-1</c> на месте
        /// <c>byte</c>. Обе стороны обязаны отказаться, а не молча взять
        /// 255 или 0.
        ///
        /// Тип исключения намеренно не сверяется: у BCL это
        /// <c>InvalidOperationException</c> с обёрнутой причиной, у XmlSerDe -
        /// <c>FormatException</c> (README: "a FormatException when a lexeme is
        /// in place but is not a number"). Совпадать они не обязаны, а вот
        /// молчаливое чтение было бы потерей данных.
        /// </summary>
        [Fact]
        public void OutOfRangeByte_Test()
        {
            const string Xml =
                "<?xml version=\"1.0\"?><TypeWithByteProperty><ByteProperty>-1</ByteProperty></TypeWithByteProperty>";

            Assert.ThrowsAny<Exception>(
                () => InteropRunner.SystemXmlDeserialize<TypeWithByteProperty>(Xml)
                );

            Assert.ThrowsAny<Exception>(
                () =>
                {
                    BclPortSerializer.Deserialize(
                        DefaultInjector.Instance,
                        BuiltinCodeHelper.CutXmlHead(Xml.AsSpan()),
                        out TypeWithByteProperty _
                        );
                });
        }

        /// <summary>
        /// <c>Xml_StringWithNullChar</c>. <c>\0</c> - не <c>Char</c> по XML 1.0
        /// §2.2, поэтому записать его нельзя, а <c>&amp;#x0;</c> в документе -
        /// ошибка well-formedness.
        ///
        /// Отказ на записи у XmlSerDe даёт <see cref="XmlFeature.CharGuard"/>,
        /// который входит в <see cref="XmlFeature.SystemXmlCompatible"/> хоста;
        /// без этого флага символ ушёл бы в документ молча - см. README,
        /// "Opt-in XML features".
        ///
        /// Вторая из двух перенесённых форм, зависящих от таргета, и снова
        /// в нашу пользу: на .NET Framework <c>XmlSerializer</c> записывает
        /// <c>\0</c> в документ, не сказав ни слова, и получившийся XML потом
        /// не читается ничем. Проверку запрещённых символов ужесточили
        /// в .NET Core.
        /// </summary>
        [Fact]
        public void StringWithNullChar_Test()
        {
            var subject = new StringWithNullCharSubject { Value = "Sample\0String" };

#if NETFRAMEWORK
            //BCL на этом таргете не отказывается, а пишет ссылку на символ, которого
            //в XML 1.0 не существует, - и получившийся документ не читает уже никто,
            //включая его самого
            var bclXml = InteropRunner.SystemXmlSerialize(subject);
            Assert.Contains("&#x0;", bclXml, StringComparison.Ordinal);
            Assert.ThrowsAny<Exception>(
                () => InteropRunner.SystemXmlDeserialize<StringWithNullCharSubject>(bclXml)
                );
#else
            Assert.ThrowsAny<Exception>(
                () => InteropRunner.SystemXmlSerialize(subject)
                );
#endif

            Assert.ThrowsAny<Exception>(
                () =>
                {
                    var exhauster = new StringBuilderExhauster();
                    BclPortSerializer.Serialize(exhauster, subject, false);
                });

            const string Xml =
                "<?xml version=\"1.0\"?><StringWithNullCharSubject><Value>Sample&#x0;String</Value></StringWithNullCharSubject>";

            Assert.ThrowsAny<Exception>(
                () => InteropRunner.SystemXmlDeserialize<StringWithNullCharSubject>(Xml)
                );

            Assert.ThrowsAny<Exception>(
                () =>
                {
                    BclPortSerializer.Deserialize(
                        DefaultInjector.Instance,
                        BuiltinCodeHelper.CutXmlHead(Xml.AsSpan()),
                        out StringWithNullCharSubject _
                        );
                });
        }
    }
}
