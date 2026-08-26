using System;
using System.Globalization;
using System.Xml;
using XmlSerDe.Common;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Span-парсеры сравниваются с BCL на том же таргете: ns2.0-ветка
    /// исполняется под net472.
    /// </summary>
    public class XmlSpanParseFixture
    {
        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("FALSE")]
        [InlineData("  false  ")]
        public void Boolean_MatchesBoolParse(string text)
        {
            Assert.Equal(bool.Parse(text), XmlSpanParse.ParseBoolean(text.AsSpan()));
        }

        [Fact]
        public void Boolean_OfGarbage_Throws()
        {
            Assert.Throws<FormatException>(() => XmlSpanParse.ParseBoolean("yes".AsSpan()));
            Assert.Throws<FormatException>(() => XmlSpanParse.ParseBoolean("1".AsSpan()));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData("  42 ")]
        [InlineData("+7")]
        [InlineData("-2147483648")]
        [InlineData("2147483647")]
        public void Int32_MatchesIntParse(string text)
        {
            Assert.Equal(
                int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                XmlSpanParse.ParseInt32(text.AsSpan())
                );
        }

        [Fact]
        public void Int32_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => XmlSpanParse.ParseInt32("2147483648".AsSpan()));
            Assert.Throws<OverflowException>(() => XmlSpanParse.ParseInt32("-2147483649".AsSpan()));
        }

        [Fact]
        public void UInt32_MinusZero_IsZero()
        {
            Assert.Equal(0u, XmlSpanParse.ParseUInt32("-0".AsSpan()));
            Assert.Equal(0u, uint.Parse("-0", NumberStyles.Integer, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void UInt32_Negative_ThrowsOverflow()
        {
            Assert.Throws<OverflowException>(() => XmlSpanParse.ParseUInt32("-1".AsSpan()));
        }

        [Fact]
        public void UInt64_MaxValue_Parses()
        {
            Assert.Equal(ulong.MaxValue, XmlSpanParse.ParseUInt64("18446744073709551615".AsSpan()));
        }

        [Fact]
        public void SByte_AndByte_MatchBcl()
        {
            Assert.Equal((sbyte)(-128), XmlSpanParse.ParseSByte("-128".AsSpan()));
            Assert.Equal((byte)255, XmlSpanParse.ParseByte("255".AsSpan()));
            Assert.Throws<OverflowException>(() => XmlSpanParse.ParseByte("256".AsSpan()));
        }
    }

    public class XmlDurationParseFixture
    {
        [Theory]
        [InlineData("PT0S")]
        [InlineData("P1DT2H3M4.005S")]
        [InlineData("-P1DT2H3M4S")]
        [InlineData("P1Y")]
        [InlineData("P1M")]
        [InlineData("P2Y10M15DT10H30M20S")]
        [InlineData("PT1H")]
        [InlineData("P0D")]
        public void ParseDuration_MatchesXmlConvert(string text)
        {
            Assert.Equal(XmlConvert.ToTimeSpan(text), XmlNumberLexis.ParseDuration(text.AsSpan()));
        }

        [Fact]
        public void ParseDuration_RoundtripsWriter()
        {
            TimeSpan[] values =
            [
                TimeSpan.Zero,
                TimeSpan.MinValue,
                TimeSpan.MaxValue,
                new TimeSpan(1, 2, 3, 4, 5),
                -new TimeSpan(1, 2, 3, 4, 5),
                TimeSpan.FromTicks(1),
            ];

            foreach (var value in values)
            {
                var lexical = XmlNumberLexis.ToDurationString(value);
                Assert.Equal(value, XmlNumberLexis.ParseDuration(lexical.AsSpan()));
            }
        }

        [Fact]
        public void ParseDuration_OfGarbage_ThrowsFormat()
        {
            Assert.Throws<FormatException>(() => XmlNumberLexis.ParseDuration("not-a-duration".AsSpan()));
            Assert.Throws<FormatException>(() => XmlNumberLexis.ParseDuration("P".AsSpan()));
            Assert.Throws<FormatException>(() => XmlNumberLexis.ParseDuration("PT".AsSpan()));
        }
    }
}
