using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Injector;
using Xunit;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Regression tests for XML 1.0 spec-compliance gaps found by manual review:
    /// unescaped '&gt;' inside attribute values, prolog constructs other than the
    /// XML declaration (processing instructions, DOCTYPE), and attribute-value
    /// whitespace normalization (XML 1.0 3.3.3).
    /// </summary>
    public class SpecComplianceFixture
    {
        // --- '>' inside an attribute value (XML 1.0 2.4: only '<', '&' and the
        // matching quote character require escaping in AttValue; '>' does not) ---

        [Fact]
        public void AttributeValue_ContainingGreaterThan_ParsesWithoutThrowing_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = "<Foo attr=\"1>2\">hello</Foo>".AsSpan();

            var node = new XmlNode2(context, xml);

            Assert.True(node.DeclaredNodeType.SequenceEqual("Foo".AsSpan()));
            Assert.True(node.Internals.SequenceEqual("hello".AsSpan()));
            Assert.False(node.IsBodyless);
        }

        [Fact]
        public void AttributeValue_ContainingGreaterThan_SelfClosingTag_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = "<Foo attr=\"a>b\"/>".AsSpan();

            var node = new XmlNode2(context, xml);

            Assert.True(node.DeclaredNodeType.SequenceEqual("Foo".AsSpan()));
            Assert.True(node.IsBodyless);
        }

        [Fact]
        public void PolymorphicDeserialize_WithExtraAttributeContainingGreaterThan_Test()
        {
            // Mirrors XmlObject4_5_Deserialize_Test0, but the element carries an
            // extra, unrelated attribute (as a foreign XML producer might) whose
            // value contains an unescaped '>'.
            XmlSerializerDeserializer4_5.Deserialize(
                DefaultInjector.Instance,
                (@"<XmlObject5><XmlObjectProperty data-note=""1>2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject4Specific1""> <StringProperty>MyString</StringProperty><IntProperty>123</IntProperty>   </XmlObjectProperty></XmlObject5>").AsSpan(),
                out XmlObject5 xo
                );

            Assert.NotNull(xo);
            var xo4specific = xo.XmlObjectProperty as XmlObject4Specific1;
            Assert.NotNull(xo4specific);
            Assert.Equal("MyString", xo4specific.StringProperty);
            Assert.Equal(123, xo4specific.IntProperty);
        }

        // --- Prolog constructs other than the XML declaration
        // (XML 1.0 2.8: prolog ::= XMLDecl? Misc* (doctypedecl Misc*)?) ---

        [Fact]
        public void Prolog_StylesheetProcessingInstruction_IsSkipped_Test()
        {
            XmlSerializerDeserializerMarkup.Deserialize(
                DefaultInjector.Instance,
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    true,
                    (@"<?xml version=""1.0"" encoding=""utf-8""?><?xml-stylesheet type=""text/xsl"" href=""s.xsl""?><XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan()
                    ),
                out XmlObject2 xo
                );

            Assert.NotNull(xo);
            Assert.Equal(123, xo.IntProperty);
        }

        [Fact]
        public void Prolog_Doctype_IsSkipped_Test()
        {
            XmlSerializerDeserializerMarkup.Deserialize(
                DefaultInjector.Instance,
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    true,
                    (@"<?xml version=""1.0"" encoding=""utf-8""?><!DOCTYPE XmlObject2 [<!ELEMENT XmlObject2 (#PCDATA)>]><XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan()
                    ),
                out XmlObject2 xo
                );

            Assert.NotNull(xo);
            Assert.Equal(123, xo.IntProperty);
        }

        [Fact]
        public void Prolog_DoctypeWithoutInternalSubset_IsSkipped_Test()
        {
            XmlSerializerDeserializerMarkup.Deserialize(
                DefaultInjector.Instance,
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    true,
                    (@"<?xml version=""1.0"" encoding=""utf-8""?><!DOCTYPE XmlObject2 SYSTEM ""XmlObject2.dtd""><XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan()
                    ),
                out XmlObject2 xo
                );

            Assert.NotNull(xo);
            Assert.Equal(123, xo.IntProperty);
        }

        [Fact]
        public void Prolog_CommentsPiAndDoctypeInterleaved_IsSkipped_Test()
        {
            XmlSerializerDeserializerFull.Deserialize(
                DefaultInjector.Instance,
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    true,
                    (@"<?xml version=""1.0"" encoding=""utf-8""?><!-- comment --><?xml-stylesheet type=""text/xsl"" href=""s.xsl""?><!DOCTYPE XmlObject2 [<!ELEMENT XmlObject2 (#PCDATA)>]><!-- another comment --><XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan()
                    ),
                out XmlObject2 xo
                );

            Assert.NotNull(xo);
            Assert.Equal(123, xo.IntProperty);
        }

        // --- Attribute-value normalization (XML 1.0 3.3.3): a *literal* tab/CR/LF
        // in an attribute value is replaced by a single space; a character
        // reference (e.g. &#10;) is expanded verbatim and must NOT be normalized. ---

        [Fact]
        public void AttributeValue_LiteralTabAndNewline_NormalizedToSingleSpace_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = "<Foo xmlns:p3=\"http://www.w3.org/2001/XMLSchema-instance\" p3:type=\"A\tB\nC\"></Foo>".AsSpan();

            var node = new XmlNode2(context, xml);
            var preciseType = node.GetPreciseNodeType();

            Assert.True(preciseType.SequenceEqual("A B C".AsSpan()));
        }

        [Fact]
        public void AttributeValue_CharacterReferenceNewline_IsNotNormalizedToSpace_Test()
        {
            var context = new XmlParseContext(false, false);
            roschar xml = "<Foo xmlns:p3=\"http://www.w3.org/2001/XMLSchema-instance\" p3:type=\"A&#10;B\"></Foo>".AsSpan();

            var node = new XmlNode2(context, xml);
            var preciseType = node.GetPreciseNodeType();

            Assert.True(preciseType.SequenceEqual("A\nB".AsSpan()));
        }
    }
}
