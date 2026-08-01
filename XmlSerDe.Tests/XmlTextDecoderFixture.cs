using System;
using System.Text;
using XmlSerDe.Common;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// XmlTextDecoder replaced WebUtility.HtmlDecode, and the two disagree on
    /// purpose, so the difference has to be pinned down:
    ///
    /// 1. HtmlDecode knows several hundred HTML entities; XML without a DTD
    ///    declares exactly five (XML 1.0 §4.6). A reference to anything else is
    ///    a well-formedness error (§4.1, WFC: Entity Declared), not text to be
    ///    passed through.
    /// 2. HtmlDecode is lenient about malformed references; XML is not.
    /// 3. A character reference must resolve to a legal Char (§2.2), which rules
    ///    out U+0000 and lone surrogates.
    ///
    /// The buffer is taken from the stack below 256 chars and from ArrayPool
    /// above it, so every shape worth testing is also tested long.
    /// </summary>
    public class XmlTextDecoderFixture
    {
        #region element text: the five predefined entities and character references

        [Fact]
        public void ElementText_WithoutMarkup_IsReturnedVerbatim()
        {
            Assert.Equal("plain text", XmlTextDecoder.DecodeElementText("plain text".AsSpan()));
            Assert.Equal("", XmlTextDecoder.DecodeElementText(roschar_Empty));
        }

        [Fact]
        public void ElementText_PredefinedEntities_AreDecoded()
        {
            Assert.Equal("&", XmlTextDecoder.DecodeElementText("&amp;".AsSpan()));
            Assert.Equal("<", XmlTextDecoder.DecodeElementText("&lt;".AsSpan()));
            Assert.Equal(">", XmlTextDecoder.DecodeElementText("&gt;".AsSpan()));
            Assert.Equal("'", XmlTextDecoder.DecodeElementText("&apos;".AsSpan()));
            Assert.Equal("\"", XmlTextDecoder.DecodeElementText("&quot;".AsSpan()));
        }

        [Fact]
        public void ElementText_EntitiesMixedWithText_AreDecodedInPlace()
        {
            Assert.Equal(
                "a<b>c&d'e\"f",
                XmlTextDecoder.DecodeElementText("a&lt;b&gt;c&amp;d&apos;e&quot;f".AsSpan())
                );
        }

        [Fact]
        public void ElementText_AdjacentEntities_AreDecoded()
        {
            //no literal run between them - the copy loop must not assume one
            Assert.Equal("<<>>", XmlTextDecoder.DecodeElementText("&lt;&lt;&gt;&gt;".AsSpan()));
        }

        [Fact]
        public void ElementText_DecimalAndHexCharacterReferences_AreDecoded()
        {
            Assert.Equal("A", XmlTextDecoder.DecodeElementText("&#65;".AsSpan()));
            Assert.Equal("A", XmlTextDecoder.DecodeElementText("&#x41;".AsSpan()));
            //hex digits are case-insensitive
            Assert.Equal("ÿ", XmlTextDecoder.DecodeElementText("&#xFF;".AsSpan()));
            Assert.Equal("ÿ", XmlTextDecoder.DecodeElementText("&#xff;".AsSpan()));
            //leading zeroes are legal
            Assert.Equal("A", XmlTextDecoder.DecodeElementText("&#0000065;".AsSpan()));
        }

        [Fact]
        public void ElementText_CharacterReferenceAboveBmp_BecomesSurrogatePair()
        {
            var decoded = XmlTextDecoder.DecodeElementText("&#x1F600;".AsSpan());

            Assert.Equal(2, decoded.Length);
            Assert.True(char.IsHighSurrogate(decoded[0]));
            Assert.True(char.IsLowSurrogate(decoded[1]));
            Assert.Equal(0x1F600, char.ConvertToUtf32(decoded[0], decoded[1]));
        }

        [Fact]
        public void ElementText_CharacterReferenceToWhitespace_IsNotCollapsed()
        {
            Assert.Equal("\n", XmlTextDecoder.DecodeElementText("&#10;".AsSpan()));
            Assert.Equal("\t", XmlTextDecoder.DecodeElementText("&#9;".AsSpan()));
        }

        #endregion

        #region element text: CDATA

        [Fact]
        public void ElementText_CDataSection_IsTakenVerbatim()
        {
            Assert.Equal(
                "<a href='x'> & </a>",
                XmlTextDecoder.DecodeElementText("<![CDATA[<a href='x'> & </a>]]>".AsSpan())
                );
        }

        [Fact]
        public void ElementText_TwoAdjacentCDataSections_AreConcatenated()
        {
            Assert.Equal(
                "firstsecond",
                XmlTextDecoder.DecodeElementText("<![CDATA[first]]><![CDATA[second]]>".AsSpan())
                );
        }

        [Fact]
        public void ElementText_CDataAfterText_IsDecoded()
        {
            //the previous implementation only recognized CDATA at the very start
            //of the body and handed everything else to HtmlDecode, so this case
            //came back with the markup still in it
            Assert.Equal(
                "before<raw>after",
                XmlTextDecoder.DecodeElementText("before<![CDATA[<raw>]]>after".AsSpan())
                );
        }

        [Fact]
        public void ElementText_EntitiesAroundCData_AreDecodedButCDataIsNot()
        {
            Assert.Equal(
                "&&amp;&",
                XmlTextDecoder.DecodeElementText("&amp;<![CDATA[&amp;]]>&amp;".AsSpan())
                );
        }

        [Fact]
        public void ElementText_UnterminatedCData_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("<![CDATA[no end".AsSpan())
                );
        }

        [Fact]
        public void ElementText_LiteralLessThan_Throws()
        {
            //XML 1.0 §2.4 forbids a literal '<' in character data
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("a < b".AsSpan())
                );
        }

        #endregion

        #region element text: references XML rejects but HtmlDecode did not

        [Fact]
        public void ElementText_UndeclaredEntity_Throws()
        {
            //HtmlDecode resolved this to U+00A0; XML without a DTD has no such
            //entity, and passing it through would be equally wrong
            var e = Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("a&nbsp;b".AsSpan())
                );
            Assert.Contains("nbsp", e.Message);
        }

        [Fact]
        public void ElementText_UnterminatedReference_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("a & b".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("trailing &".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&amp".AsSpan())
                );
        }

        [Fact]
        public void ElementText_EmptyReference_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&;".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#;".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#x;".AsSpan())
                );
        }

        [Fact]
        public void ElementText_UppercaseXInCharacterReference_Throws()
        {
            //CharRef ::= '&#' [0-9]+ ';' | '&#x' [0-9a-fA-F]+ ';' - the marker is
            //lowercase 'x' only
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#X41;".AsSpan())
                );
        }

        [Fact]
        public void ElementText_NonDigitInCharacterReference_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#6a5;".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#xZZ;".AsSpan())
                );
        }

        [Fact]
        public void ElementText_CharacterReferenceToIllegalChar_Throws()
        {
            //U+0000 and the C0 controls other than TAB/LF/CR are not legal Chars
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#0;".AsSpan())
                );
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#1;".AsSpan())
                );
            //a lone surrogate is not a Char either
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#xD800;".AsSpan())
                );
            //U+FFFE and U+FFFF are excluded by the Char production
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#xFFFF;".AsSpan())
                );
        }

        [Fact]
        public void ElementText_CharacterReferenceOutOfUnicodeRange_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#x110000;".AsSpan())
                );
            //must not overflow into a legal-looking value on the way
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeElementText("&#99999999999999999999;".AsSpan())
                );
        }

        #endregion

        #region attribute values: XML 1.0 §3.3.3 normalization

        [Fact]
        public void AttributeValue_LiteralWhitespace_BecomesSpaces()
        {
            Assert.Equal("a b c d", XmlTextDecoder.DecodeAttributeValue("a\tb\rc\nd".AsSpan()));
        }

        [Fact]
        public void AttributeValue_WhitespaceFromCharacterReference_IsKept()
        {
            //step (1) applies to literal whitespace in the source only; a
            //reference is expanded afterwards and inserted verbatim
            Assert.Equal("a\nb", XmlTextDecoder.DecodeAttributeValue("a&#10;b".AsSpan()));
            Assert.Equal("a\tb", XmlTextDecoder.DecodeAttributeValue("a&#x9;b".AsSpan()));
        }

        [Fact]
        public void AttributeValue_LiteralWhitespaceAndReferencesTogether_AreBothHandled()
        {
            Assert.Equal(
                "x <y>\nz",
                XmlTextDecoder.DecodeAttributeValue("x\t&lt;y&gt;&#10;z".AsSpan())
                );
        }

        [Fact]
        public void AttributeValue_UndeclaredEntity_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => XmlTextDecoder.DecodeAttributeValue("&copy;".AsSpan())
                );
        }

        #endregion

        #region the pooled path (input longer than the stack threshold)

        [Fact]
        public void ElementText_LongerThanStackThreshold_DecodesIdentically()
        {
            var source = new StringBuilder();
            var expected = new StringBuilder();
            for (var i = 0; i < 200; i++)
            {
                source.Append("ab&amp;<![CDATA[<c>]]>&#65;");
                expected.Append("ab&<c>A");
            }

            Assert.True(source.Length > 256);
            Assert.Equal(
                expected.ToString(),
                XmlTextDecoder.DecodeElementText(source.ToString().AsSpan())
                );
        }

        [Fact]
        public void AttributeValue_LongerThanStackThreshold_DecodesIdentically()
        {
            var source = new StringBuilder();
            var expected = new StringBuilder();
            for (var i = 0; i < 200; i++)
            {
                source.Append("ab\t&quot;&#10;");
                expected.Append("ab \"\n");
            }

            Assert.True(source.Length > 256);
            Assert.Equal(
                expected.ToString(),
                XmlTextDecoder.DecodeAttributeValue(source.ToString().AsSpan())
                );
        }

        #endregion

        private static ReadOnlySpan<char> roschar_Empty => ReadOnlySpan<char>.Empty;
    }
}
