using System;
using XmlSerDe;
using XmlSerDe.Internal;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Guards the behaviours that only exist because deserialization became
    /// single-pass: the parse now reports how much input it consumed instead of
    /// measuring every node in advance, so the caller's cursor depends on that
    /// number being exact. Three things follow, and none of them was reachable
    /// by the previous implementation:
    ///
    /// 1. An element with no matching member is skipped by counting tag balance
    ///    (XmlScan.SkipBody). That path never ran before - every element used to
    ///    be fully parsed - and it must stay quote-aware.
    /// 2. A self-closing child no longer terminates the sibling loop.
    /// 3. The body span is no longer clipped on the right, so a mismatched
    ///    closing tag is the only thing standing between the parser and the rest
    ///    of the document.
    ///
    /// Documents here are written without indentation on purpose: an off-by-one
    /// in the consumed length is absorbed by whitespace between elements and
    /// would go unnoticed on a pretty-printed document.
    /// </summary>
    public class SinglePassParserFixture
    {
        #region unknown elements are skipped, not parsed

        [Fact]
        public void UnknownElement_BetweenKnownOnes_IsSkipped()
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><Unknown>whatever</Unknown><StringProperty>abc</StringProperty><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal("abc", xo.StringProperty);
            Assert.Equal(7, xo.IntProperty);
        }

        [Fact]
        public void UnknownElement_WithNestedChildren_IsSkippedWholesale()
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><Unknown><A><B>1</B><C/></A><D>2</D></Unknown><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal(7, xo.IntProperty);
        }

        [Fact]
        public void UnknownElement_WithGreaterThanInsideAttributeValue_IsSkipped()
        {
            //XML 1.0 2.4: '>' needs no escaping inside AttValue, so a skip that
            //counts tag balance must not treat this one as the end of the head
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><Unknown attr=\"a>b\"><Inner attr='c>d'>x</Inner></Unknown><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal(7, xo.IntProperty);
        }

        [Fact]
        public void UnknownSelfClosingElement_WithGreaterThanInsideAttributeValue_IsSkipped()
        {
            //a self-closing tag must not increase the nesting depth
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><Unknown attr=\"a>b\"/><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal(7, xo.IntProperty);
        }

        [Fact]
        public void UnknownElement_ContainingCData_IsSkipped()
        {
            //'<' inside CDATA must not be mistaken for a tag
            XmlSerializerDeserializer33.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject33><Unknown><![CDATA[</Unknown><IntProperty>]]></Unknown><CDataString><![CDATA[kept]]></CDataString></XmlObject33>".AsSpan(),
                out XmlObject33 xo
                );

            Assert.Equal("kept", xo.CDataString);
        }

        #endregion

        #region self-closing children no longer swallow their siblings

        [Fact]
        public void SelfClosingChild_DoesNotTerminateTheSiblingLoop()
        {
            //the previous implementation returned length 0 for a bodyless node,
            //which the generated loop read as "no more children" - everything
            //after it was silently dropped
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><StringProperty/><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            //закрытая нода без xsi:nil - это пустое значение, а не отсутствие значения
            Assert.Equal("", xo.StringProperty);
            Assert.Equal(7, xo.IntProperty);
        }

        [Fact]
        public void SelfClosingChild_WithAttributes_DoesNotTerminateTheSiblingLoop()
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><StringProperty xsi:nil=\"true\" /><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );

            //а вот с xsi:nil="true" значения действительно нет, и член остаётся null
            Assert.Null(xo.StringProperty);
            Assert.Equal(7, xo.IntProperty);
        }

        #endregion

        #region the closing-tag check is the only right-hand boundary left

        [Fact]
        public void MismatchedClosingTag_Throws()
        {
            Assert.Throws<XmlDocumentException>(() =>
                XmlSerializerDeserializer2.Deserialize(
                    DefaultInjector.Instance,
                    "<XmlObject2><StringProperty>abc</WrongName><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                    out XmlObject2 _
                    )
                );
        }

        [Fact]
        public void TruncatedDocument_Throws()
        {
            Assert.Throws<XmlDocumentException>(() =>
                XmlSerializerDeserializer2.Deserialize(
                    DefaultInjector.Instance,
                    "<XmlObject2><StringProperty>abc".AsSpan(),
                    out XmlObject2 _
                    )
                );
        }

        #endregion

        #region compact documents round-trip

        [Fact]
        public void CompactAndIndentedDocuments_ParseIdentically()
        {
            //an off-by-one in the consumed length is invisible on an indented
            //document and fatal on a compact one, so both must be checked
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                "<XmlObject2><StringProperty>abc</StringProperty><IntProperty>7</IntProperty></XmlObject2>".AsSpan(),
                out XmlObject2 compact
                );

            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                @"<XmlObject2>
    <StringProperty>abc</StringProperty>
    <IntProperty>7</IntProperty>
</XmlObject2>".AsSpan(),
                out XmlObject2 indented
                );

            Assert.Equal(compact.StringProperty, indented.StringProperty);
            Assert.Equal(compact.IntProperty, indented.IntProperty);
        }

        #endregion
    }
}
