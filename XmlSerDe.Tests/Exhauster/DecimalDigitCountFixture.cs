using System.Globalization;
using XmlSerDe.Components.Exhauster;
using Xunit;

namespace XmlSerDe.Tests.Exhauster
{
    /// <summary>
    /// Log-таблица - верхняя оценка числа десятичных цифр, никогда ниже
    /// <c>ToString</c>. Знак минус оценщик считает отдельно.
    /// </summary>
    public class DecimalDigitCountFixture
    {
        [Fact]
        public void UInt32_NeverUnderestimates()
        {
            foreach (var value in UInt32Samples())
            {
                var actual = value.ToString(CultureInfo.InvariantCulture).Length;
                Assert.True(DecimalDigitsLog.AtLeast(value) >= actual, "log " + value);
            }
        }

        [Fact]
        public void UInt64_NeverUnderestimates()
        {
            foreach (var value in UInt64Samples())
            {
                var actual = value.ToString(CultureInfo.InvariantCulture).Length;
                Assert.True(DecimalDigitsLog.AtLeast(value) >= actual, "log " + value);
            }
        }

        [Fact]
        public void SignedAppend_CountsMinusAndDigits()
        {
            var estimator = new LengthEstimatorExhauster();
            AssertSigned32(estimator);
            AssertSigned64(estimator);
        }

        private static void AssertSigned32(LengthEstimatorExhauster estimator)
        {
            int[] values = [int.MinValue, -1, 0, 1, 9, 10, 99, int.MaxValue];
            foreach (var value in values)
            {
                estimator.Clear();
                estimator.Append(value);
                var actual = value.ToString(CultureInfo.InvariantCulture).Length;
                Assert.True(
                    estimator.EstimatedTotalLength >= actual,
                    "int " + value
                    );
            }
        }

        private static void AssertSigned64(LengthEstimatorExhauster estimator)
        {
            long[] values = [long.MinValue, -1L, 0L, 1L, 9L, 10L, 99L, long.MaxValue];
            foreach (var value in values)
            {
                estimator.Clear();
                estimator.Append(value);
                var actual = value.ToString(CultureInfo.InvariantCulture).Length;
                Assert.True(
                    estimator.EstimatedTotalLength >= actual,
                    "long " + value
                    );
            }
        }

        private static System.Collections.Generic.IEnumerable<uint> UInt32Samples()
        {
            yield return 0;
            yield return 1;
            yield return 9;
            yield return 10;
            uint pow10 = 1;
            for (var k = 0; k < 9; k++)
            {
                yield return pow10 - 1;
                yield return pow10;
                yield return pow10 + 1;
                pow10 *= 10;
            }

            yield return uint.MaxValue;
            yield return (uint)int.MaxValue;
            yield return unchecked((uint)int.MinValue);
            for (var bit = 0; bit < 32; bit++)
            {
                yield return 1u << bit;
            }
        }

        private static System.Collections.Generic.IEnumerable<ulong> UInt64Samples()
        {
            yield return 0;
            yield return 1;
            yield return 9;
            yield return 10;
            ulong pow10 = 1;
            for (var k = 0; k < 19; k++)
            {
                yield return pow10 - 1;
                yield return pow10;
                yield return pow10 + 1;
                pow10 *= 10;
            }

            yield return ulong.MaxValue;
            yield return (ulong)long.MaxValue;
            yield return unchecked((ulong)long.MinValue);
            for (var bit = 0; bit < 64; bit++)
            {
                yield return 1ul << bit;
            }
        }
    }
}
