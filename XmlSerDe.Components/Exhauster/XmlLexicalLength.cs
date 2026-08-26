using System;
using System.Runtime.CompilerServices;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// Длина лексической формы без построения строки. DateTime - формат писателя
    /// <c>yyyy-MM-ddTHH:mm:ss.FFFFFFFK</c>: 19 символов до дроби, до 8 на
    /// <c>.FFFFFFF</c>, суффикс Kind - пусто / <c>Z</c> / <c>±HH:mm</c>.
    /// Потолок этой формы - 33 (Local + семь знаков дроби), не 28: 28 - это UTC
    /// с полной дробью, без смещения зоны.
    /// </summary>
    public static class XmlLexicalLength
    {
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DateTime(DateTime value)
        {
            var n = 19;
            var fraction = value.Ticks % System.TimeSpan.TicksPerSecond;
            if (fraction != 0)
            {
                n += 1 + CountTrimmedFractionDigits((ulong)fraction);
            }

            switch (value.Kind)
            {
                case DateTimeKind.Utc:
                    n += 1;
                    break;
                case DateTimeKind.Local:
                    n += 6;
                    break;
            }

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TimeSpan(TimeSpan value)
            => XmlNumberLexis.DurationCharCount(value);

        public static int Decimal(decimal value)
        {
#if NET8_0_OR_GREATER
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            var lo = (uint)bits[0];
            var mid = (uint)bits[1];
            var hi = (uint)bits[2];
            var flags = bits[3];
#else
            var layout = new DecimalLayout { Value = value };
            var lo = (uint)layout.Lo;
            var mid = (uint)layout.Mid;
            var hi = (uint)layout.Hi;
            var flags = layout.Flags;
#endif
            var scale = (byte)((flags >> 16) & 0xFF);
            var negative = flags < 0;
            var mantissaZero = lo == 0 && mid == 0 && hi == 0;

            if (mantissaZero)
            {
                // ToString нуля без минуса, scale сохраняется: 0.00m → "0.00"
                return scale == 0 ? 1 : 2 + scale;
            }

            var digits = CountDigits96(lo, mid, hi);
            var n = negative ? 1 : 0;
            if (scale == 0)
            {
                return n + digits;
            }

            if (digits > scale)
            {
                return n + digits + 1;
            }

            return n + 2 + scale;
        }

        private static int CountTrimmedFractionDigits(ulong fraction)
        {
            var digits = 7;
            while (fraction % 10 == 0)
            {
                fraction /= 10;
                digits--;
            }

            return digits;
        }

        private static int CountDigits32(uint value)
        {
            if (value < 10) return 1;
            if (value < 100) return 2;
            if (value < 1000) return 3;
            if (value < 10000) return 4;
            if (value < 100000) return 5;
            if (value < 1000000) return 6;
            if (value < 10000000) return 7;
            if (value < 100000000) return 8;
            if (value < 1000000000) return 9;
            return 10;
        }

        private static int CountDigits64(ulong value)
        {
            if (value < 10) return 1;
            if (value < 100) return 2;
            if (value < 1000) return 3;
            if (value < 10000) return 4;
            if (value < 100000) return 5;
            if (value < 1000000) return 6;
            if (value < 10000000) return 7;
            if (value < 100000000) return 8;
            if (value < 1000000000) return 9;
            if (value < 10000000000) return 10;
            if (value < 100000000000) return 11;
            if (value < 1000000000000) return 12;
            if (value < 10000000000000) return 13;
            if (value < 100000000000000) return 14;
            if (value < 1000000000000000) return 15;
            if (value < 10000000000000000) return 16;
            if (value < 100000000000000000) return 17;
            if (value < 1000000000000000000) return 18;
            if (value < 10000000000000000000) return 19;
            return 20;
        }

        private static int CountDigits96(uint lo, uint mid, uint hi)
        {
            if (hi == 0)
            {
                if (mid == 0)
                {
                    return CountDigits32(lo);
                }

                return CountDigits64(((ulong)mid << 32) | lo);
            }

            var mantissa = new decimal(unchecked((int)lo), unchecked((int)mid), unchecked((int)hi), false, 0);
            if (mantissa >= 10000000000000000000000000000m) return 29;
            if (mantissa >= 1000000000000000000000000000m) return 28;
            if (mantissa >= 100000000000000000000000000m) return 27;
            if (mantissa >= 10000000000000000000000000m) return 26;
            if (mantissa >= 1000000000000000000000000m) return 25;
            if (mantissa >= 100000000000000000000000m) return 24;
            if (mantissa >= 10000000000000000000000m) return 23;
            if (mantissa >= 1000000000000000000000m) return 22;
            if (mantissa >= 100000000000000000000m) return 21;
            return 20;
        }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Раскладка <see cref="decimal"/> совпадает с <c>GetBits</c>:
        /// flags, hi, lo, mid. На ns2.0 <c>GetBits</c> выделяет <c>int[4]</c>.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct DecimalLayout
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public decimal Value;
            [System.Runtime.InteropServices.FieldOffset(0)] public int Flags;
            [System.Runtime.InteropServices.FieldOffset(4)] public int Hi;
            [System.Runtime.InteropServices.FieldOffset(8)] public int Lo;
            [System.Runtime.InteropServices.FieldOffset(12)] public int Mid;
        }
#endif
    }
}
