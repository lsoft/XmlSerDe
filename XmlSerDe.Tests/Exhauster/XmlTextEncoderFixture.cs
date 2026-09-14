using System;
using System.Net;
using System.Text;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;
using Xunit;

namespace XmlSerDe.Tests.Exhauster
{
    /// <summary>
    /// Экранирование текста тела совпадает с <see cref="WebUtility.HtmlEncode"/>,
    /// но без промежуточной строки.
    /// </summary>
    public class XmlTextEncoderFixture
    {
        [Fact]
        public void Encode_MatchesHtmlEncode_ForSpecialsAndLatin1()
        {
            string[] values =
            [
                "",
                "plain",
                SerDeFixture.RawString,
                "a<b>c&d\"e'f",
                "\t\r\n",
                "я",
                "\u00A0\u00A9\u00FF",
                "😊",
            ];

            foreach (var value in values)
            {
                Assert.Equal(WebUtility.HtmlEncode(value), XmlTextEncoder.Encode(value));
            }
        }

        /// <summary>
        /// Непарный суррогат - один символ U+FFFD, как у HtmlEncode; и в начале,
        /// и после обычного текста, и вторым после первого попадания.
        /// </summary>
        [Fact]
        public void Encode_LoneSurrogate_BecomesReplacementCharacter()
        {
            //не Theory: xunit сериализует данные InlineData, и строка с непарным
            //суррогатом доходит до теста уже испорченной
            (string value, string expected)[] cases =
            [
                ("\uD83D", "�"),
                ("lone\uD83Dsurrogate", "lone�surrogate"),
                ("a<b\uDC00c", "a&lt;b�c"),
                ("\uD83D\U0001F600", "�&#128512;"),
            ];

            foreach (var (value, expected) in cases)
            {
                Assert.Equal(expected, XmlTextEncoder.Encode(value));

                //Unchecked: со стражем непарный суррогат отвергается как не-Char ещё
                //до кодирования, и это отдельно проверенное поведение XmlCharGuard
                var sb = new StringBuilderExhauster();
                sb.AppendEncodedUnchecked(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public void Encode_ReturnsSameInstance_WhenNothingToEscape()
        {
            var value = "plain-text";
            Assert.Same(value, XmlTextEncoder.Encode(value));
        }

        [Fact]
        public void AppendEncoded_MatchesHtmlEncode()
        {
            var sb = new StringBuilder();
            var exhauster = new StringBuilderExhauster(sb);
            exhauster.AppendEncoded(SerDeFixture.RawString);
            Assert.Equal(WebUtility.HtmlEncode(SerDeFixture.RawString), sb.ToString());
        }

        [Fact]
        public void AppendEncoded_Utf8_MatchesStringBuilder()
        {
            var sb = new StringBuilderExhauster();
            sb.AppendEncoded("a<b>c&d\"e'f\u00A9😊");

            using var ms = new System.IO.MemoryStream();
            using (var utf8 = new Utf8BinaryExhausterStream(ms))
            {
                utf8.AppendEncoded("a<b>c&d\"e'f\u00A9😊");
            }

            Assert.Equal(sb.ToString(), Encoding.UTF8.GetString(ms.ToArray()));
        }
    }

    public class XmlAttributeEncoderSinkFixture
    {
        [Fact]
        public void Append_MatchesEncode()
        {
            string[] values =
            [
                "plain",
                "a<b>c&d\"e",
                "line\r\n\t",
            ];

            foreach (var value in values)
            {
                var sb = new StringBuilder();
                XmlAttributeEncoder.Append(sb, value);
                Assert.Equal(XmlAttributeEncoder.Encode(value), sb.ToString());
            }
        }

        [Fact]
        public void Utf8_MatchesStringBuilder()
        {
            const string Value = "a<b>&\"\r\n\t";
            var sb = new StringBuilderExhauster();
            sb.AppendAttributeEncoded(Value);

            using var ms = new System.IO.MemoryStream();
            using (var utf8 = new Utf8BinaryExhausterStream(ms))
            {
                utf8.AppendAttributeEncoded(Value);
            }

            Assert.Equal(sb.ToString(), Encoding.UTF8.GetString(ms.ToArray()));
        }

        /// <summary>
        /// Раньше после первого экранируемого символа остаток писался по одному
        /// символу, и UTF-8 сток кодировал половинки суррогатной пары порознь -
        /// два U+FFFD вместо эмодзи. Пакетная запись держит пару вместе.
        /// </summary>
        [Theory]
        [InlineData("<\U0001F600")]
        [InlineData("a\"b\U0001F600c")]
        [InlineData("\U0001F600&\U0001F600")]
        [InlineData("x\r\n\U0001F600")]
        public void Utf8_SurrogatePairAfterEscapable_IsNotSplit(string value)
        {
            var sb = new StringBuilderExhauster();
            sb.AppendAttributeEncoded(value);

            using var ms = new System.IO.MemoryStream();
            using (var utf8 = new Utf8BinaryExhausterStream(ms))
            {
                utf8.AppendAttributeEncoded(value);
            }

            var bytes = ms.ToArray();
            Assert.Equal(Encoding.UTF8.GetBytes(sb.ToString()), bytes);
            Assert.DoesNotContain((byte)0xEF, bytes);
            Assert.Contains((byte)0xF0, bytes);
        }
    }

    /// <summary>
    /// Оценщик длины даёт ровно ту длину, что напишет кодировщик, а не оценку
    /// «+4 за спецсимвол»: та не знала <c>&amp;quot;</c>, латиницу 160..255
    /// и суррогатные пары, и второй проход рос с копированием буфера.
    /// </summary>
    public class LengthEstimatorExactnessFixture
    {
        [Theory]
        [InlineData("plain")]
        [InlineData("a\"b'c<d>e&f")]
        [InlineData("©®é")]
        [InlineData("\U0001F600")]
        [InlineData("\U0010FFFF\U00010000")]
        [InlineData("lone\uD83Dsurrogate")]
        [InlineData("mixed \"©\U0001F600<")]
        public void AppendEncoded_EstimateIsExact(string value)
        {
            var estimator = new LengthEstimatorExhauster();
            estimator.AppendEncoded(value);

            var sb = new StringBuilderExhauster();
            sb.AppendEncoded(value);

            Assert.Equal(sb.ToString().Length, estimator.EstimatedTotalLength);
        }

        [Theory]
        [InlineData("plain")]
        [InlineData("a\"b<c>d&e")]
        [InlineData("tab\tcr\rlf\n")]
        [InlineData("©\U0001F600")]
        public void AppendAttributeEncoded_EstimateIsExact(string value)
        {
            var estimator = new LengthEstimatorExhauster();
            estimator.AppendAttributeEncoded(value);

            var sb = new StringBuilderExhauster();
            sb.AppendAttributeEncoded(value);

            Assert.Equal(sb.ToString().Length, estimator.EstimatedTotalLength);
        }
    }
}
