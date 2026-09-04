using System;
using XmlSerDe.Common;
using Xunit;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Контекст разбора едет одним параметром: <see cref="XmlParseContext"/>
    /// несёт и эвристики документа, и найденное предком имя xsi-префикса. Отдельного
    /// параметра <c>xmlnsAttributeName</c> у <see cref="IInjector.Parse"/> и
    /// <see cref="XmlNode2"/> больше нет - см. README, "Breaking change
    /// (custom injector)".
    /// </summary>
    public class XmlParseContextFixture
    {
        private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        [Fact]
        public void XmlnsAttributeName_FoundInOwnHead_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = ("<Base xmlns:p3=\"" + XsiNamespace + "\" p3:type=\"Child\">x</Base>").AsSpan();

            var node = new XmlNode2(context, xml);

            Assert.True(node.XmlnsAttributeName.SequenceEqual("p3".AsSpan()));
            Assert.True(node.GetPreciseNodeType().SequenceEqual("Child".AsSpan()));
        }

        /// <summary>
        /// Голова наследника объявления xmlns не повторяет - имя префикса приходит
        /// из контекста, и это единственный способ его туда положить.
        /// </summary>
        [Fact]
        public void XmlnsAttributeName_InheritedFromSettings_Test()
        {
            var context = new XmlParseContext(false, false)
                .WithXmlnsAttributeName("p3".AsSpan());
            roschar xml = "<Base p3:type=\"Child\">x</Base>".AsSpan();

            var node = new XmlNode2(context, xml);

            Assert.True(node.XmlnsAttributeName.SequenceEqual("p3".AsSpan()));
            Assert.True(node.GetPreciseNodeType().SequenceEqual("Child".AsSpan()));
        }

        /// <summary>
        /// Без имени в контексте тот же документ точного типа не даёт: скан головы
        /// объявления не находит. Так проверяется, что предыдущий тест меряет
        /// именно контекст, а не самостоятельный разбор.
        /// </summary>
        [Fact]
        public void XmlnsAttributeName_AbsentEverywhere_YieldsNoPreciseType_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = "<Base p3:type=\"Child\">x</Base>".AsSpan();

            var node = new XmlNode2(context, xml);

            Assert.True(node.XmlnsAttributeName.IsEmpty);
            Assert.True(node.GetPreciseNodeType().IsEmpty);
        }

        /// <summary>
        /// Производный контекст - копия со всеми прочими полями: спуск имени в
        /// поддерево не должен по дороге терять эвристики документа.
        /// </summary>
        [Fact]
        public void WithXmlnsAttributeName_KeepsOtherFields_Test()
        {
            var context = new XmlParseContext(true, true)
                .WithXmlnsAttributeName("p3".AsSpan());

            Assert.True(context.ContainsXmlComments);
            Assert.True(context.ContainsCDataBlocks);
            Assert.True(context.XmlnsAttributeName.SequenceEqual("p3".AsSpan()));
        }

        /// <summary>
        /// Исходный контекст производный не трогает: readonly ref struct копируется,
        /// а не мутируется.
        /// </summary>
        [Fact]
        public void WithXmlnsAttributeName_DoesNotTouchSource_Test()
        {
            var source = new XmlParseContext(false, false);
            var derived = source.WithXmlnsAttributeName("p3".AsSpan());

            Assert.True(source.XmlnsAttributeName.IsEmpty);
            Assert.True(derived.XmlnsAttributeName.SequenceEqual("p3".AsSpan()));
        }
    }
}
