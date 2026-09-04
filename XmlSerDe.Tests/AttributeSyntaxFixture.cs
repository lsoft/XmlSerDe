using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Injector;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Синтаксис атрибута по XML 1.0 §3.1: <c>Attribute ::= Name Eq AttValue</c>,
    /// <c>Eq ::= S? '=' S?</c>.
    ///
    /// Проверки живут в <see cref="XmlScan"/> и не являются opt-in
    /// (docs/opt-in-xml-guards.md §5.1): у ядра нет режима, в котором читать
    /// значение соседнего атрибута было бы правильно. Поэтому здесь два уровня:
    /// разбор головы напрямую и один и тот же документ на хосте без фич
    /// и на хосте с <see cref="XmlFeature.SystemXmlCompatible"/> - оба обязаны
    /// вести себя одинаково.
    ///
    /// Половина случаев ниже - не про строгость, а про потерю данных: до этой
    /// проверки имя атрибута обрывалось только на '=', ':', '/' и '&gt;', так что
    /// легальный <c>a = "1"</c> давал имя <c>"a "</c> (с пробелом) и не совпадал
    /// ни с одним членом, а <c>id=1 Tag="x"</c> - значение <c>"x"</c>, взятое
    /// у следующего атрибута.
    /// </summary>
    public class AttributeSyntaxFixture
    {
        #region легальные формы Eq

        [Theory]
        [InlineData("<Foo a=\"1\" b=\"2\"/>")]
        [InlineData("<Foo a = \"1\" b=\"2\"/>")]
        [InlineData("<Foo a =\"1\" b=\"2\"/>")]
        [InlineData("<Foo a= \"1\" b=\"2\"/>")]
        [InlineData("<Foo a\t=\t\"1\" b=\"2\"/>")]
        [InlineData("<Foo a\r\n=\r\n\"1\" b=\"2\"/>")]
        public void SpaceAroundEq_IsLegalAndFound_Test(string head)
        {
            Assert.Equal("1", ValueOf(head, "a"));
            Assert.Equal("2", ValueOf(head, "b"));
        }

        [Theory]
        [InlineData("<Foo a='1'/>")]
        [InlineData("<Foo a = '1'/>")]
        public void SingleQuotedValue_IsFound_Test(string head)
        {
            Assert.Equal("1", ValueOf(head, "a"));
        }

        [Theory]
        [InlineData("<Foo p:a=\"1\"/>")]
        [InlineData("<Foo p:a = \"1\"/>")]
        public void PrefixedName_IsFound_Test(string head)
        {
            Assert.Equal("1", ValueOf(head, "a", "p"));
        }

        [Fact]
        public void EmptyValue_IsFound_Test()
        {
            Assert.Equal("", ValueOf("<Foo a=\"\" b=\"2\"/>", "a"));
        }

        [Fact]
        public void ValueWithGreaterThanInside_IsFound_Test()
        {
            //голова quote-aware всегда, это не фича (opt-in-xml-features.md §17)
            Assert.Equal("a>b", ValueOf("<Foo a=\"a>b\" b=\"2\"/>", "a"));
            Assert.Equal("2", ValueOf("<Foo a=\"a>b\" b=\"2\"/>", "b"));
        }

        #endregion

        #region битый синтаксис - ошибка о документе, а не молча «не то»

        [Theory]
        //нет кавычек, дальше конец головы
        [InlineData("<Foo id=1/>")]
        //нет кавычек, дальше ещё атрибут: кавычка бралась у соседа, id получал "x"
        [InlineData("<Foo id=1 Tag=\"x\"/>")]
        //нет '=': имя разбиралось как "id Tag", оба члена оставались пустыми
        [InlineData("<Foo id Tag=\"x\"/>")]
        //нет закрывающей кавычки
        [InlineData("<Foo id=\"1/>")]
        //значение начинается не с кавычки
        [InlineData("<Foo id=x\"1\"/>")]
        //пробел вокруг ':' в QName недопустим
        [InlineData("<Foo p :a=\"1\"/>")]
        public void BrokenSyntax_Throws_Test(string head)
        {
            Assert.Throws<XmlDocumentException>(() => ValueOf(head, "id"));
        }

        [Fact]
        public void BrokenSyntax_MessageIsInSystemXmlForm_Test()
        {
            var noEq = Assert.Throws<XmlDocumentException>(
                () => ValueOf("<Foo id Tag=\"x\"/>", "id")
                );
            Assert.Equal(
                "'Tag' is an unexpected token. The expected token is '='.",
                noEq.Message
                );

            var noQuote = Assert.Throws<XmlDocumentException>(
                () => ValueOf("<Foo id=1 Tag=\"x\"/>", "id")
                );
            Assert.Equal(
                "'1' is an unexpected token. The expected token is '\"' or '''.",
                noQuote.Message
                );
        }

        #endregion

        #region то же самое через сгенерированный разбор, на обоих семействах хостов

        private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// Хост без <c>[XmlFeatures]</c>: пробелы вокруг '=' в <c>xsi:type</c>
        /// легальны, и до этого исправления атрибут просто не находился -
        /// документ читался так, будто точного типа не указали вовсе.
        /// </summary>
        [Fact]
        public void DefaultHost_SpaceAroundEq_ResolvesXsiType_Test()
        {
            XmlSerializerDeserializer4_5.Deserialize(
                DefaultInjector.Instance,
                ("<XmlObject5><XmlObjectProperty xmlns:xsi = \"" + Xsi + "\" xsi:type = \"XmlObject4Specific1\">"
                + "<StringProperty>MyString</StringProperty><IntProperty>123</IntProperty>"
                + "</XmlObjectProperty></XmlObject5>").AsSpan(),
                out XmlObject5 result
                );

            var specific = Assert.IsType<XmlObject4Specific1>(result.XmlObjectProperty);
            Assert.Equal("MyString", specific.StringProperty);
            Assert.Equal(123, specific.IntProperty);
        }

        [Fact]
        public void DefaultHost_BrokenAttributeSyntax_Throws_Test()
        {
            Assert.Throws<XmlDocumentException>(
                () => XmlSerializerDeserializer4_5.Deserialize(
                    DefaultInjector.Instance,
                    ("<XmlObject5><XmlObjectProperty xmlns:xsi=\"" + Xsi + "\" xsi:type=XmlObject4Specific1>"
                    + "<StringProperty>MyString</StringProperty>"
                    + "</XmlObjectProperty></XmlObject5>").AsSpan(),
                    out XmlObject5 _
                    )
                );
        }

        /// <summary>
        /// Хост с полным набором фич ведёт себя ровно так же: синтаксис атрибута
        /// не фича и не страж.
        /// </summary>
        [Fact]
        public void FeatureHost_BrokenAttributeSyntax_Throws_Test()
        {
            Assert.Throws<XmlDocumentException>(
                () => Interop.InteropSerializer.Deserialize(
                    DefaultInjector.Instance,
                    "<AttributeSubject id=1 Tag=\"x\" kind=\"zero-value\" flag=\"false\" note=\"n\" />".AsSpan(),
                    out Interop.Subject.AttributeSubject _
                    )
                );
        }

        [Fact]
        public void FeatureHost_SpaceAroundEq_IsRead_Test()
        {
            Interop.InteropSerializer.Deserialize(
                DefaultInjector.Instance,
                "<AttributeSubject id = \"7\" Tag = \"x\" kind=\"zero-value\" flag=\"false\" note=\"n\" />".AsSpan(),
                out Interop.Subject.AttributeSubject result
                );

            Assert.Equal(7, result.Id);
            Assert.Equal("x", result.Tag);
        }

        #endregion

        /// <summary>
        /// Значение атрибута в голове или null, если атрибута нет. Индекс -
        /// конец имени тега, ровно как его передаёт сгенерированный код.
        /// </summary>
        private static string? ValueOf(string head, string name, string prefix = "")
        {
            XmlScan.ParseAttribute(
                head.AsSpan(),
                head.IndexOf(' '),
                prefix.AsSpan(),
                name.AsSpan(),
                ReadOnlySpan<char>.Empty,
                out var parsed
                );

            return parsed.IsEmpty ? null : parsed.Value.ToString();
        }
    }
}
