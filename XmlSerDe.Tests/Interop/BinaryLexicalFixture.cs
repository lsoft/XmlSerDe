#nullable disable

using System;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Interop.Subject;
using Xunit;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Формы, которые дифференциальная сверка выдать не может: она гоняет объект
    /// через обе реализации, а здесь проверяется чтение документов, которых ни одна
    /// из них не пишет. Пустая лексема и лишние пробелы вокруг base64 - как раз такие:
    /// написать их своим писателем нельзя, а прийти извне они могут запросто.
    ///
    /// Ожидание в каждом случае снято с самого BCL и тут же и перепроверяется:
    /// утверждается не "у нас получилось X", а "у обеих сторон получилось одно и то же".
    /// </summary>
    public class BinaryLexicalFixture
    {
        private static BinarySubject ReadBoth(string xml)
        {
            InteropSerializer.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out BinarySubject ours);

            var theirs = InteropRunner.SystemXmlDeserialize<BinarySubject>(xml);

            Assert.Equal(theirs.Bytes, ours.Bytes);
            Assert.Equal(theirs.Empty, ours.Empty);
            Assert.Equal(theirs.Missing, ours.Missing);
            Assert.Equal(theirs.After, ours.After);

            return ours;
        }

        /// <summary>
        /// Пустое тело - это <c>byte[0]</c>, а не null. Отсутствие массива BCL
        /// выражает не пустым элементом, а отсутствием элемента вовсе.
        /// </summary>
        [Fact]
        public void EmptyBody_Test()
        {
            var result = ReadBoth("<BinarySubject><Bytes></Bytes><After>1</After></BinarySubject>");

            Assert.NotNull(result.Bytes);
            Assert.Empty(result.Bytes);
        }

        [Fact]
        public void BodylessElement_Test()
        {
            var result = ReadBoth("<BinarySubject><Bytes /><After>1</After></BinarySubject>");

            Assert.NotNull(result.Bytes);
            Assert.Empty(result.Bytes);
        }

        /// <summary>
        /// Отсутствующий элемент - единственный способ получить null.
        /// </summary>
        [Fact]
        public void MissingElement_Test()
        {
            var result = ReadBoth("<BinarySubject><After>1</After></BinarySubject>");

            Assert.Null(result.Bytes);
        }

        /// <summary>
        /// Пробельные символы внутри лексемы игнорируются: base64 разрешено
        /// разбивать на строки, и читатель обязан такую запись понять.
        /// </summary>
        [Fact]
        public void WhitespaceAroundPayload_Test()
        {
            var result = ReadBoth("<BinarySubject><Bytes>  AQL6  </Bytes><After>1</After></BinarySubject>");

            Assert.Equal(new byte[] { 1, 2, 250, }, result.Bytes);
        }

        [Fact]
        public void SplitPayload_Test()
        {
            var result = ReadBoth("<BinarySubject><Bytes>AQ\r\n L6</Bytes><After>1</After></BinarySubject>");

            Assert.Equal(new byte[] { 1, 2, 250, }, result.Bytes);
        }
    }
}
