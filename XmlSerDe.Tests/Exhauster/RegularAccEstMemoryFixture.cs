using System.Net;
using System.Text;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex;
using Xunit;

namespace XmlSerDe.Tests.Exhauster
{
    /// <summary>
    /// На REGULAR оценщик был ниже факта на 4 символа: <c>WebUtility.HtmlEncode</c>
    /// пишет апостроф как <c>&amp;#39;</c> (+4), а оценщик апостроф не считал,
    /// и <c>StringBuilder</c> удваивал буфер.
    /// </summary>
    public class RegularAccEstMemoryFixture
    {
        [Fact]
        public void HtmlEncode_Apostrophe_IsFourExtraChars()
        {
            Assert.Equal("&#39;", WebUtility.HtmlEncode("'"));
            Assert.Equal("&#39;", XmlTextEncoder.Encode("'"));
        }

        [Fact]
        public void AccEst_CountsApostropheOverhead()
        {
            var estimator = new LengthEstimatorExhauster();
            estimator.AppendEncoded("'");
            Assert.Equal(5, estimator.EstimatedTotalLength);
        }

        [Fact]
        public void Regular_AccEst_IsNotBelowSerializedLength()
        {
            var xml = ComplexFixture.Serialize_XmlSerDe(ComplexFixture.DefaultObject);

            var accEst = new LengthEstimatorExhauster();
            XmlSerializerDeserializer.Serialize(accEst, ComplexFixture.DefaultObject, false);

            Assert.True(
                accEst.EstimatedTotalLength >= xml.Length,
                "estimator " + accEst.EstimatedTotalLength + " < actual " + xml.Length
                );

            var sb = new StringBuilder(accEst.EstimatedTotalLength);
            var writer = new StringBuilderExhauster(sb);
            XmlSerializerDeserializer.Serialize(writer, ComplexFixture.DefaultObject, false);
            Assert.Equal(xml.Length, writer.ToString().Length);
            Assert.Equal(accEst.EstimatedTotalLength, sb.Capacity);

            using var pooled = new PooledCharExhauster(accEst.EstimatedTotalLength);
            XmlSerializerDeserializer.Serialize(pooled, ComplexFixture.DefaultObject, false);
            Assert.Equal(xml, pooled.ToString());
            Assert.Equal(xml.Length, pooled.Written);
        }
    }
}
