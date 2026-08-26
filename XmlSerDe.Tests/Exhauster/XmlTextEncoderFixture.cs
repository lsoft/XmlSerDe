using System;
using System.Net;
using System.Text;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
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
    }
}
