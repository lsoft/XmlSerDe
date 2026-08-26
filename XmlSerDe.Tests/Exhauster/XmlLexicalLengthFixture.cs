using System;
using System.Globalization;
using System.Text;
using System.Xml;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using Xunit;

namespace XmlSerDe.Tests.Exhauster
{
    /// <summary>
    /// Длина DateTime / TimeSpan / decimal совпадает с тем, что пишет экзостер,
    /// без запаса в 33/30.
    /// </summary>
    public class XmlLexicalLengthFixture
    {
        [Fact]
        public void DateTime_MatchesWriterFormat()
        {
            DateTime[] values =
            [
                DateTime.MinValue,
                DateTime.MaxValue,
                new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc),
                new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567),
                new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Unspecified).AddTicks(1234567),
                new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Local).AddTicks(1234567),
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1000000),
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1230000),
                new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ];

            foreach (var value in values)
            {
                var actual = value.ToString(XmlLexicalLength.DateTimeFormat, CultureInfo.InvariantCulture).Length;
                Assert.Equal(actual, XmlLexicalLength.DateTime(value));
            }
        }

        [Fact]
        public void DateTime_LocalWithFraction_Is33_Not28()
        {
            var value = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Local).AddTicks(1234567);
            var actual = value.ToString(XmlLexicalLength.DateTimeFormat, CultureInfo.InvariantCulture);
            Assert.Equal(33, actual.Length);
            Assert.Equal(33, XmlLexicalLength.DateTime(value));
        }

        [Fact]
        public void TimeSpan_MatchesXmlConvertAndWriter()
        {
            TimeSpan[] values =
            [
                TimeSpan.Zero,
                TimeSpan.MinValue,
                TimeSpan.MaxValue,
                TimeSpan.FromDays(1),
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(1),
                new TimeSpan(1, 2, 3, 4, 5),
                -new TimeSpan(1, 2, 3, 4, 5),
                TimeSpan.FromTicks(1),
                TimeSpan.FromTicks(1000000),
                TimeSpan.FromTicks(1230000),
            ];

            Span<char> buffer = stackalloc char[32];
            foreach (var value in values)
            {
                var written = XmlNumberLexis.ToDurationString(value);
                Assert.Equal(written, XmlConvert.ToString(value));
                Assert.Equal(written.Length, XmlNumberLexis.DurationCharCount(value));
                Assert.Equal(written.Length, XmlLexicalLength.TimeSpan(value));

                Assert.True(XmlNumberLexis.TryFormat(buffer, value, out var n));
                Assert.Equal(written, buffer.Slice(0, n).ToString());

                var sb = new StringBuilder();
                XmlNumberLexis.Append(sb, value);
                Assert.Equal(written, sb.ToString());
            }
        }

        [Fact]
        public void Decimal_MatchesInvariantToString()
        {
            decimal[] values =
            [
                0m,
                0.00m,
                -0.00m,
                1m,
                -1m,
                1.50m,
                99.99m,
                0.001m,
                0.0000000000000000000000000001m,
                decimal.MinValue,
                decimal.MaxValue,
                18446744073709551616m,
            ];

            foreach (var value in values)
            {
                var actual = value.ToString(CultureInfo.InvariantCulture).Length;
                Assert.Equal(actual, XmlLexicalLength.Decimal(value));
            }
        }

        [Fact]
        public void Estimator_AppendsExactLexicalLength()
        {
            var estimator = new LengthEstimatorExhauster();

            var date = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567);
            estimator.Append(date);
            Assert.Equal(XmlLexicalLength.DateTime(date), estimator.EstimatedTotalLength);

            estimator.Clear();
            var duration = TimeSpan.MinValue;
            estimator.Append(duration);
            Assert.Equal(27, estimator.EstimatedTotalLength);

            estimator.Clear();
            estimator.Append(decimal.MinValue);
            Assert.Equal(30, estimator.EstimatedTotalLength);
        }
    }
}
