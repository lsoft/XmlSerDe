using XmlSerDe.Common;
using XmlSerDe.Generator.Producer;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Flags → primitive names, without Roslyn. Regression "CData is on but the
    /// host still calls Decode without CDATA" is caught here — see
    /// docs/opt-in-xml-features.md §4.5 / §10.3.
    /// </summary>
    public class HostFeatureBindingFixture
    {
        [Fact]
        public void None_SelectsDefaultPrimitives()
        {
            var b = HostFeatureBinding.From(XmlFeature.None);

            Assert.Equal(nameof(XmlScan.ReadHead), b.ReadHead);
            Assert.Equal("", b.ReadHeadPrefixArgs);
            Assert.Equal(nameof(XmlScan.ReadTextBody), b.ReadTextBody);
            Assert.Equal(nameof(XmlScan.SkipBody), b.SkipBody);
            Assert.Equal("", b.SkipBodyPrefixArgs);
            Assert.Equal(nameof(XmlHead.GetXsiType), b.GetPreciseNodeType);
            Assert.Equal(nameof(XmlHead.IsXsiNil), b.IsNil);
            Assert.Equal(nameof(XmlTextDecoder.DecodeElementText), b.DecodeElementText);
            Assert.False(b.CharGuard);
        }

        [Fact]
        public void Markup_SelectsMarkupPrimitives_WithoutCData()
        {
            var b = HostFeatureBinding.From(XmlFeature.Markup);

            Assert.Equal(nameof(XmlScan.ReadHeadMarkup), b.ReadHead);
            Assert.Equal("false, false, ", b.ReadHeadPrefixArgs);
            Assert.Equal(nameof(XmlScan.ReadTextBodyMarkup), b.ReadTextBody);
            Assert.Equal("false, ", b.ReadTextBodyPrefixArgs);
            Assert.Equal(nameof(XmlScan.SkipBodyMarkup), b.SkipBody);
            Assert.Equal("false, ", b.SkipBodyPrefixArgs);
            Assert.Equal(nameof(XmlTextDecoder.DecodeElementText), b.DecodeElementText);
        }

        [Fact]
        public void CData_SelectsCDataDecoderAndTextBody()
        {
            var b = HostFeatureBinding.From(XmlFeature.CData);

            Assert.Equal(nameof(XmlScan.ReadHeadMarkup), b.ReadHead);
            Assert.Equal("true, false, ", b.ReadHeadPrefixArgs);
            Assert.Equal(nameof(XmlScan.ReadTextBodyMarkup), b.ReadTextBody);
            Assert.Equal("true, ", b.ReadTextBodyPrefixArgs);
            Assert.Equal(nameof(XmlTextDecoder.DecodeElementTextWithCData), b.DecodeElementText);
        }

        /// <summary>
        /// CData включает в себя Markup: признать CDATA и споткнуться о
        /// комментарий значило бы платить за разметочный путь, не получая его.
        /// </summary>
        [Fact]
        public void CData_Implies_Markup()
        {
            Assert.True(HostFeatureBinding.From(XmlFeature.CData).CData);
            Assert.Equal(
                XmlFeature.Markup,
                XmlFeature.CData & XmlFeature.Markup
                );
            Assert.False(HostFeatureBinding.From(XmlFeature.Markup).CData);
        }

        [Fact]
        public void FlexibleXsiPrefix_SelectsFlexibleTypeAndNil()
        {
            var b = HostFeatureBinding.From(XmlFeature.FlexibleXsiPrefix);

            Assert.Equal(nameof(XmlScan.ReadHeadFlexibleXsi), b.ReadHead);
            Assert.Equal("", b.ReadHeadPrefixArgs);
            Assert.Equal(nameof(XmlHead.GetPreciseNodeType), b.GetPreciseNodeType);
            Assert.Equal(nameof(XmlHead.IsNil), b.IsNil);
        }

        /// <summary>
        /// Кавычки атрибутов больше не ось: любой хост читает <c>'</c> и
        /// <c>"</c> и не рвёт голову на '&gt;' внутри значения.
        /// </summary>
        [Fact]
        public void QuotedAttributes_AreNotAFeature_DefaultXsiIsQuoteAware()
        {
            var b = HostFeatureBinding.From(XmlFeature.None);

            Assert.Equal(nameof(XmlHead.GetXsiType), b.GetPreciseNodeType);
            Assert.Equal(nameof(XmlHead.IsXsiNil), b.IsNil);
        }

        /// <summary>
        /// Строка без CData по-прежнему идёт через инжектор; обход декодером
        /// появляется только вместе с фичей.
        /// </summary>
        [Fact]
        public void ParseBodyStatement_GoesThroughInjector_UnlessCData()
        {
            var none = HostFeatureBinding.From(XmlFeature.None);
            var cdata = HostFeatureBinding.From(XmlFeature.CData);

            Assert.Contains(
                "inj.ParseBody(childText, out string injr);",
                none.ParseBodyStatement("childText", true, "string", "injr")
                );
            Assert.Contains(
                nameof(XmlTextDecoder.DecodeElementTextWithCData),
                cdata.ParseBodyStatement("childText", true, "string", "injr")
                );
            Assert.Contains(
                "inj.ParseBody(bodyText, out int parsed);",
                cdata.ParseBodyStatement("bodyText", false, "int", "parsed")
                );
        }

        /// <summary>
        /// Markup без CData инжектор не обходит: обход - цена именно CDATA,
        /// у которой у <c>IInjector</c> перегрузки нет.
        /// </summary>
        [Fact]
        public void ParseBodyStatement_MarkupAloneKeepsInjector()
        {
            var markup = HostFeatureBinding.From(XmlFeature.Markup);

            Assert.Contains(
                "inj.ParseBody(childText, out string injr);",
                markup.ParseBodyStatement("childText", true, "string", "injr")
                );
        }

        /// <summary>
        /// Обе ветки записи строки идут через exhauster: выключенный CharGuard
        /// снимает проверку Char, а не превращает запись в аллоцирующий
        /// <c>Append(Encode(...))</c>.
        /// </summary>
        [Fact]
        public void AppendEncodedStatement_AlwaysGoesThroughExhauster()
        {
            var none = HostFeatureBinding.From(XmlFeature.None);
            var guarded = HostFeatureBinding.From(XmlFeature.CharGuard);

            Assert.Equal(
                "exh.AppendEncodedUnchecked(value);",
                none.AppendEncodedStatement("exh", "value")
                );
            Assert.Equal(
                "exh.AppendAttributeEncodedUnchecked(value);",
                none.AppendAttributeEncodedStatement("exh", "value")
                );
            Assert.Equal(
                "exh.AppendEncoded(value);",
                guarded.AppendEncodedStatement("exh", "value")
                );
            Assert.Equal(
                "exh.AppendAttributeEncoded(value);",
                guarded.AppendAttributeEncodedStatement("exh", "value")
                );
        }

        /// <summary>
        /// CutXmlHead получает флаг хоста, а не default-набор.
        /// </summary>
        [Fact]
        public void CutXmlHeadInvocation_CarriesHostFlags()
        {
            Assert.Equal("xml", HostFeatureBinding.From(XmlFeature.None).CutXmlHeadInvocation("xml"));
            Assert.Equal("xml", HostFeatureBinding.From(XmlFeature.CharGuard).CutXmlHeadInvocation("xml"));
            Assert.Equal(
                "true, xml",
                HostFeatureBinding.From(XmlFeature.Markup).CutXmlHeadInvocation("xml")
                );
            Assert.Equal(
                "true, xml",
                HostFeatureBinding.From(XmlFeature.SystemXmlCompatible).CutXmlHeadInvocation("xml")
                );
        }

        [Fact]
        public void CharGuard_DoesNotChangeReadHead()
        {
            var b = HostFeatureBinding.From(XmlFeature.CharGuard);

            Assert.Equal(nameof(XmlScan.ReadHead), b.ReadHead);
            Assert.True(b.CharGuard);
            Assert.Contains("AppendEncoded", b.AppendEncodedStatement("exh", "value"));
        }

        [Fact]
        public void SystemXmlCompatible_SelectsMarkupCDataFlexibleChecked()
        {
            var b = HostFeatureBinding.From(XmlFeature.SystemXmlCompatible);

            Assert.Equal(nameof(XmlScan.ReadHeadMarkup), b.ReadHead);
            Assert.Equal("true, true, ", b.ReadHeadPrefixArgs);
            Assert.Equal(nameof(XmlScan.ReadTextBodyMarkup), b.ReadTextBody);
            Assert.Equal(nameof(XmlHead.GetPreciseNodeType), b.GetPreciseNodeType);
            Assert.Equal(nameof(XmlTextDecoder.DecodeElementTextWithCData), b.DecodeElementText);
            Assert.True(b.CharGuard);
        }
    }
}
