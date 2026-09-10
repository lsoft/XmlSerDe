#nullable disable

using System;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;
using BclXmlSerializer = System.Xml.Serialization.XmlSerializer;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Расхождения фасада с <see cref="BclXmlSerializer"/>, найденные ревью
    /// 2026-09-10. Эталон в каждом тесте - сам штатный сериализатор на том же входе.
    /// </summary>
    public class CompatReviewFixture
    {
        private const string Xsi = "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";

        private static CompatSubject CreateSubject() =>
            new CompatSubject
            {
                Number = 7,
                Name = "review",
                Values = new System.Collections.Generic.List<int> { 1, 2 }
            };

        #region вызов через базовый тип обязан идти быстрым путём

        /// <summary>
        /// Базовый класс подставляет свои <c>xsi</c>/<c>xsd</c>, когда вызывающий
        /// пространств имён не передавал, и фасад принимал их за чужие - каждый
        /// вызов через базовый тип уходил в штатный сериализатор. Документ через
        /// базовый тип обязан совпадать с документом через фасад.
        /// </summary>
        [Fact]
        public void BaseTypedSerialize_ProducesTheFacadeDocument()
        {
            var facade = new XmlSerializer(typeof(CompatSubject));
            BclXmlSerializer baseTyped = facade;

            var viaFacade = new StringWriter();
            facade.Serialize(viaFacade, CreateSubject());

            var viaBase = new StringWriter();
            baseTyped.Serialize(viaBase, CreateSubject());

            Assert.Equal(viaFacade.ToString(), viaBase.ToString());
            Assert.DoesNotContain("xmlns:xsd", viaBase.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// <see cref="XmlWriter"/> с <see cref="ConformanceLevel.Auto"/> объявление сам
        /// не пишет; BCL вызывает <c>WriteStartDocument</c>, пока писатель в
        /// состоянии Start, - и документ получает объявление. Без него документ
        /// через базовый тип отличался бы от документа через фасад.
        /// </summary>
        [Fact]
        public void BaseTypedSerialize_ThroughAutoConformanceWriter_WritesDeclaration()
        {
            BclXmlSerializer baseTyped = new XmlSerializer(typeof(CompatSubject));

            var sb = new StringBuilder();
            using (var xw = XmlWriter.Create(sb, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            {
                baseTyped.Serialize(xw, CreateSubject());
            }

            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", sb.ToString(), StringComparison.Ordinal);
            Assert.Contains("<Name>review</Name>", sb.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Писатель с кодировкой, в которой не всякий символ представим: BCL
        /// экранирует его числовой ссылкой, а сырой текст в такой писатель просто
        /// не пройдёт. Такой вызов обязан уйти штатным путём и дать тот же документ.
        /// </summary>
        [Fact]
        public void BaseTypedSerialize_ThroughAsciiWriter_MatchesBcl()
        {
            var subject = CreateSubject();
            subject.Name = "café";

            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            BclXmlSerializer baseTyped = new XmlSerializer(typeof(CompatSubject));

            Assert.Equal(
                WriteAscii(bcl, subject),
                WriteAscii(baseTyped, subject)
                );
        }

        private static string WriteAscii(BclXmlSerializer serializer, object subject)
        {
            var ms = new MemoryStream();
            using (var xw = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Encoding.ASCII }))
            {
                serializer.Serialize(xw, subject);
            }

            return Encoding.ASCII.GetString(ms.ToArray());
        }

        #endregion

        #region Deserialize(Stream) и кодировка документа

        /// <summary>
        /// Поток без BOM, с объявленной кодировкой: BCL её читает, фасад брал UTF-8
        /// и молча подставлял U+FFFD.
        /// </summary>
        [Fact]
        public void DeserializeStream_HonoursDeclaredLatin1()
        {
            var latin1 = Encoding.GetEncoding(28591);
            var bytes = latin1.GetBytes(
                "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><CompatSubject><Number>1</Number><Name>éà</Name></CompatSubject>"
                );

            var bcl = (CompatSubject)new BclXmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes));
            var facade = (CompatSubject)new XmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes));

            Assert.Equal("éà", bcl.Name);
            Assert.Equal(bcl.Name, facade.Name);
        }

        [Fact]
        public void DeserializeStream_DetectsUtf16WithoutBom()
        {
            var bytes = new UnicodeEncoding(bigEndian: false, byteOrderMark: false).GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-16\"?><CompatSubject><Number>1</Number><Name>é</Name></CompatSubject>"
                );

            var bcl = (CompatSubject)new BclXmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes));
            var facade = (CompatSubject)new XmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes));

            Assert.Equal(bcl.Name, facade.Name);
        }

        [Fact]
        public void DeserializeStream_Utf8WithBom_StillWorks()
        {
            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes("<CompatSubject><Number>1</Number><Name>é</Name></CompatSubject>"));

            var facade = (CompatSubject)new XmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes));

            Assert.Equal("é", facade.Name);
        }

        /// <summary>
        /// Невалидный UTF-8 - ошибка документа у BCL, а не U+FFFD в значении.
        /// </summary>
        [Fact]
        public void DeserializeStream_InvalidUtf8_ThrowsLikeBcl()
        {
            var bytes = Encoding.ASCII.GetBytes("<CompatSubject><Number>1</Number><Name>a")
                .Concat(new byte[] { 0xFF })
                .Concat(Encoding.ASCII.GetBytes("b</Name></CompatSubject>"));

            Assert.Throws<InvalidOperationException>(
                () => new BclXmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes))
                );
            Assert.Throws<InvalidOperationException>(
                () => new XmlSerializer(typeof(CompatSubject)).Deserialize(new MemoryStream(bytes))
                );
        }

        #endregion

        #region сброс буфера после записи

        /// <summary>
        /// BCL завершает запись <c>XmlWriter.Flush()</c>, который доходит до
        /// <see cref="StreamWriter"/> и до потока под ним. Без этого
        /// <c>Serialize(new StreamWriter(stream), o)</c> оставляет поток пустым.
        /// </summary>
        [Fact]
        public void SerializeTextWriter_FlushesToTheStream()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            new XmlSerializer(typeof(CompatSubject)).Serialize(writer, CreateSubject());

            Assert.True(stream.Length > 0, "nothing reached the stream");
            Assert.Contains("<Name>review</Name>", Encoding.UTF8.GetString(stream.ToArray()), StringComparison.Ordinal);
        }

        [Fact]
        public void SerializeStream_FlushesTheStream()
        {
            var stream = new FlushCountingStream();

            new XmlSerializer(typeof(CompatSubject)).Serialize(stream, CreateSubject());

            Assert.True(stream.Flushes >= 1, "Stream.Flush was never called");
        }

        private sealed class FlushCountingStream : MemoryStream
        {
            public int Flushes { get; private set; }

            public override void Flush()
            {
                Flushes++;
                base.Flush();
            }
        }

        #endregion

        #region корень с xsi:nil и телом

        /// <summary>
        /// <c>xsi:nil="true"</c> - это null и на элементе с пустым телом, а не только
        /// на закрытом: BCL пропускает содержимое и возвращает null.
        /// </summary>
        [Theory]
        [InlineData("<CompatSubject " + Xsi + " xsi:nil=\"true\"></CompatSubject>")]
        [InlineData("<CompatSubject " + Xsi + " xsi:nil=\"true\">\n  </CompatSubject>")]
        [InlineData("<CompatSubject " + Xsi + " xsi:nil=\"true\" />")]
        public void NilRoot_WithOrWithoutBody_IsNull(string xml)
        {
            Assert.Null(new BclXmlSerializer(typeof(CompatSubject)).Deserialize(new StringReader(xml)));
            Assert.Null(new XmlSerializer(typeof(CompatSubject)).Deserialize(new StringReader(xml)));
        }

        #endregion
    }

    internal static class ByteArrayExtensions
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
