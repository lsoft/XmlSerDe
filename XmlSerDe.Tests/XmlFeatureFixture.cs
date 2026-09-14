using System;
using System.Collections.Generic;
using XmlSerDe;
using XmlSerDe.Internal;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Per-flag matrix from docs/opt-in-xml-features.md §10.1–§10.2.
    /// Default hosts here are the existing serializers without [XmlFeatures]
    /// after that attribute is stripped from POCO tests.
    /// </summary>
    public class XmlFeatureFixture
    {
        private const string Poco =
            "<XmlObject2><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        private const string Comment =
            "<XmlObject2><!-- c --><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        private const string CData =
            "<XmlObject2><IntProperty>7</IntProperty><StringProperty><![CDATA[abc]]></StringProperty></XmlObject2>";

        private const string PI =
            "<XmlObject2><?pi d?><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        private const string DoctypeProlog =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><!DOCTYPE XmlObject2 [<!ELEMENT XmlObject2 (#PCDATA)>]><XmlObject2><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        //посторонний атрибут в одинарных кавычках на читаемой голове: раньше
        //это требовало отдельного флага, теперь читается любым хостом
        private const string SingleQuoted =
            "<XmlObject2 note='x'><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        private const string GtInAttribute =
            "<XmlObject2><Unknown attr=\"1>2\"/><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

        private const string P3Type =
            "<XmlObject5><XmlObjectProperty xmlns:p3=\"http://www.w3.org/2001/XMLSchema-instance\" p3:type=\"XmlObject4Specific1\"><StringProperty>MyString</StringProperty><IntProperty>123</IntProperty></XmlObjectProperty></XmlObject5>";

        private const string XsiType =
            "<XmlObject5><XmlObjectProperty xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:type=\"XmlObject4Specific1\"><StringProperty>MyString</StringProperty><IntProperty>123</IntProperty></XmlObjectProperty></XmlObject5>";

        [Fact]
        public void Default_RoundTripPoco_Succeeds()
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                Poco.AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal(7, xo.IntProperty);
            Assert.Equal("abc", xo.StringProperty);
        }

        [Fact]
        public void Default_IllegalChar_DoesNotThrowFromCharGuard()
        {
            var sb = new StringBuilderExhauster();
            var obj = new XmlObject2 { StringProperty = "a\u0001b", IntProperty = 1 };
            XmlSerializerDeserializer2.Serialize(sb, obj, false);
            Assert.Contains("\u0001", sb.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Default_LegalSpecials_StillEntityEscaped()
        {
            var sb = new StringBuilderExhauster();
            var obj = new XmlObject2 { StringProperty = "a<b&c>", IntProperty = 1 };
            XmlSerializerDeserializer2.Serialize(sb, obj, false);
            var xml = sb.ToString();
            Assert.Contains("a&lt;b&amp;c&gt;", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("a<b", xml, StringComparison.Ordinal);
        }

        /// <summary>
        /// null внутри коллекции строк - не ошибка сериализации ни при каком
        /// наборе фич: <c>CharGuard</c> про запрещённые символы, а не про null.
        /// </summary>
        [Fact]
        public void NullStringInsideCollection_IsWrittenAsEmptyElement()
        {
            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer6.Serialize(
                sb,
                new XmlObject6 { StringsProperty = new List<string> { "a", null!, "b" } },
                false
                );

            Assert.Equal(
                "<XmlObject6><StringsProperty><string>a</string><string></string><string>b</string></StringsProperty></XmlObject6>",
                sb.ToString()
                );
        }

        /// <summary>
        /// Строковый член без <see cref="XmlFeature.CData"/> по-прежнему
        /// проходит через <see cref="IInjector"/>: фича, которую не включали,
        /// не должна незаметно выкидывать пользовательский инжектор из разбора.
        /// </summary>
        [Fact]
        public void Default_StringMember_GoesThroughInjector()
        {
            var injector = new SuffixInjector();

            XmlSerializerDeserializerCustomInjector.Deserialize(
                injector,
                Poco.AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal("abc!", xo.StringProperty);
            Assert.Equal(1, injector.StringCalls);
        }

        /// <summary>
        /// <see cref="XmlFeature.Markup"/> сам по себе инжектор не обходит:
        /// прямое декодирование - цена именно <see cref="XmlFeature.CData"/>,
        /// у которой у <see cref="IInjector"/> перегрузки нет.
        /// </summary>
        [Fact]
        public void Markup_StringMember_StillGoesThroughInjector()
        {
            var injector = new SuffixInjector();

            XmlSerializerDeserializerMarkupCustomInjector.Deserialize(
                injector,
                Comment.AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal("abc!", xo.StringProperty);
            Assert.Equal(1, injector.StringCalls);
        }

        /// <summary>
        /// Кавычки атрибутов и '&gt;' внутри AttValue (XML 1.0 §2.4) - не фича:
        /// default-хост обязан это понимать, иначе голова обрезается молча
        /// и неправильно.
        /// </summary>
        [Fact]
        public void Default_QuoteAwareHead_IsNotOptIn()
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                SingleQuoted.AsSpan(),
                out XmlObject2 single
                );
            Assert.Equal(7, single.IntProperty);

            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                GtInAttribute.AsSpan(),
                out XmlObject2 gt
                );
            Assert.Equal(7, gt.IntProperty);
        }

        #region матрица §10.2

        //колонки: комментарий, CDATA, PI, DOCTYPE, attr='x', attr="1>2", p3:type, xsi:type
        //
        //Двух колонок с кавычками матрица больше не различает по хостам: разбор
        //атрибутов quote-aware у всех, поэтому '+' в них стоит уже у default.
        private static readonly string[] Documents =
        {
            "comment",
            "cdata",
            "pi",
            "doctype",
            "singleQuoted",
            "gtInAttribute",
            "p3type",
            "xsitype",
        };

        public static IEnumerable<object[]> MatrixRows()
        {
            yield return new object[] { "default", "----++-+" };
            yield return new object[] { "Markup", "+-++++-+" };
            yield return new object[] { "CData", "++++++-+" };
            yield return new object[] { "FlexibleXsi", "----++++" };
            yield return new object[] { "Full", "++++++++" };
        }

        /// <summary>
        /// Строка матрицы целиком: '+' - документ прочитан и значения те же,
        /// '-' - исключение либо не тот смысл (§5.1 разрешает и то и другое).
        /// </summary>
        [Theory]
        [MemberData(nameof(MatrixRows))]
        public void Matrix_Row(string host, string expected)
        {
            Assert.Equal(Documents.Length, expected.Length);

            for (var i = 0; i < Documents.Length; i++)
            {
                var document = Documents[i];
                var accepted = Reads(host, document);
                var actual = accepted ? '+' : '-';

                Assert.True(
                    actual == expected[i],
                    $"{host} × {document}: ожидалось '{expected[i]}', получено '{actual}'"
                    );
            }
        }

        private static bool Reads(string host, string document)
        {
            try
            {
                switch (document)
                {
                    case "comment":
                        return ReadsObject2(host, Comment);
                    case "cdata":
                        return ReadsObject2(host, CData);
                    case "pi":
                        return ReadsObject2(host, PI);
                    case "doctype":
                        return ReadsObject2(host, CutProlog(host, DoctypeProlog));
                    case "singleQuoted":
                        return ReadsObject2(host, SingleQuoted);
                    case "gtInAttribute":
                        return ReadsObject2(host, GtInAttribute);
                    case "p3type":
                        return ReadsObject5(host, P3Type);
                    case "xsitype":
                        return ReadsObject5(host, XsiType);
                    default:
                        throw new InvalidOperationException("unknown document " + document);
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string CutProlog(string host, string xml)
        {
            switch (host)
            {
                case "default":
                    return XmlSerializerDeserializer2.CutXmlHead(xml.AsSpan()).ToString();
                case "Markup":
                    return XmlSerializerDeserializerMarkup.CutXmlHead(xml.AsSpan()).ToString();
                case "CData":
                    return XmlSerializerDeserializerCData.CutXmlHead(xml.AsSpan()).ToString();
                case "FlexibleXsi":
                    return XmlSerializerDeserializerFlexibleXsi.CutXmlHead(xml.AsSpan()).ToString();
                case "Full":
                    return XmlSerializerDeserializerFull.CutXmlHead(xml.AsSpan()).ToString();
                default:
                    throw new InvalidOperationException("unknown host " + host);
            }
        }

        private static bool ReadsObject2(string host, string xml)
        {
            XmlObject2 xo;
            var inj = DefaultInjector.Instance;

            switch (host)
            {
                case "default":
                    XmlSerializerDeserializer2.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "Markup":
                    XmlSerializerDeserializerMarkup.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "CData":
                    XmlSerializerDeserializerCData.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "FlexibleXsi":
                    XmlSerializerDeserializerFlexibleXsi.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "Full":
                    XmlSerializerDeserializerFull.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                default:
                    throw new InvalidOperationException("unknown host " + host);
            }

            return xo is not null
                && xo.IntProperty == 7
                && xo.StringProperty == "abc";
        }

        private static bool ReadsObject5(string host, string xml)
        {
            XmlObject5 xo;
            var inj = DefaultInjector.Instance;

            switch (host)
            {
                case "default":
                    XmlSerializerDeserializer4_5.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "Markup":
                    XmlSerializerDeserializerMarkup.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "CData":
                    XmlSerializerDeserializerCData.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "FlexibleXsi":
                    XmlSerializerDeserializerFlexibleXsi.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                case "Full":
                    XmlSerializerDeserializerFull.Deserialize(inj, xml.AsSpan(), out xo);
                    break;
                default:
                    throw new InvalidOperationException("unknown host " + host);
            }

            return xo?.XmlObjectProperty is XmlObject4Specific1 derived
                && derived.StringProperty == "MyString"
                && derived.IntProperty == 123;
        }

        #endregion

        [Fact]
        public void CharGuard_ThrowsOnIllegalChar_AllowsTabCrLf()
        {
            var sb = new StringBuilderExhauster();
            Assert.Throws<ArgumentException>(
                () => XmlSerializerDeserializerCharGuard.Serialize(
                    sb,
                    new XmlObject2 { StringProperty = "a\u0001b" },
                    false
                    )
                );

            var ok = new StringBuilderExhauster();
            XmlSerializerDeserializerCharGuard.Serialize(
                ok,
                new XmlObject2 { StringProperty = "a\tb\rc\nd", IntProperty = 1 },
                false
                );
            Assert.Contains("a\tb", ok.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TwoAttributes_OrTogetherLikeOne()
        {
            XmlSerializerDeserializerTwoAttributes.Deserialize(
                DefaultInjector.Instance,
                Comment.AsSpan(),
                out XmlObject2 a
                );
            XmlSerializerDeserializerOrFlags.Deserialize(
                DefaultInjector.Instance,
                Comment.AsSpan(),
                out XmlObject2 b
                );
            Assert.Equal(a.IntProperty, b.IntProperty);

            XmlSerializerDeserializerTwoAttributes.Deserialize(
                DefaultInjector.Instance,
                CData.AsSpan(),
                out XmlObject2 c
                );
            XmlSerializerDeserializerOrFlags.Deserialize(
                DefaultInjector.Instance,
                CData.AsSpan(),
                out XmlObject2 d
                );
            Assert.Equal(c.StringProperty, d.StringProperty);
        }

        /// <summary>
        /// Комментарий - это разметка между текстом, а не признак его конца:
        /// System.Xml.Serialization отдаёт текст по обе стороны комментария
        /// склеенным. Раньше «ведущий» комментарий искался по первому
        /// <c>&lt;</c> где угодно в теле, и всё, что стояло до него, терялось.
        /// </summary>
        [Theory]
        [InlineData("<XmlObject2><StringProperty>hello<!-- c --></StringProperty><IntProperty>1</IntProperty></XmlObject2>", "hello")]
        [InlineData("<XmlObject2><StringProperty>hello<!-- c -->world</StringProperty><IntProperty>1</IntProperty></XmlObject2>", "helloworld")]
        [InlineData("<XmlObject2><StringProperty><!-- a -->hello<!-- b -->world<!-- c --></StringProperty><IntProperty>1</IntProperty></XmlObject2>", "helloworld")]
        [InlineData("<XmlObject2><StringProperty>a &amp; b<!-- c -->c</StringProperty><IntProperty>1</IntProperty></XmlObject2>", "a & bc")]
        public void Markup_CommentInsideText_KeepsTextOnBothSides(string xml, string expected)
        {
            XmlSerializerDeserializerMarkup.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject2 xo
                );

            Assert.Equal(expected, xo.StringProperty);
            Assert.Equal(1, xo.IntProperty);
        }
    }
}
