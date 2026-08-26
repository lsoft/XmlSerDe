using System;
using System.Runtime.CompilerServices;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Разбор лексических форм из спана там, где на netstandard2.0 нет
    /// <c>Parse(ReadOnlySpan&lt;char&gt;)</c>. Поведение совпадает с
    /// <c>Parse(..., NumberStyles.Integer, InvariantCulture)</c> и
    /// <see cref="bool.Parse(string)"/>: те же исключения по типу, не по тексту.
    /// </summary>
    public static class XmlSpanParse
    {
        public static bool ParseBoolean(ReadOnlySpan<char> body)
        {
            var trimmed = body.Trim();
            if (EqualsOrdinalIgnoreCase(trimmed, "true".AsSpan()))
            {
                return true;
            }
            if (EqualsOrdinalIgnoreCase(trimmed, "false".AsSpan()))
            {
                return false;
            }

            throw new FormatException("The input is not a valid boolean lexical form.");
        }

        public static sbyte ParseSByte(ReadOnlySpan<char> body)
            => checked((sbyte)ParseInt64(body, sbyte.MinValue, sbyte.MaxValue));

        public static byte ParseByte(ReadOnlySpan<char> body)
            => checked((byte)ParseUInt64(body, byte.MaxValue));

        public static short ParseInt16(ReadOnlySpan<char> body)
            => checked((short)ParseInt64(body, short.MinValue, short.MaxValue));

        public static ushort ParseUInt16(ReadOnlySpan<char> body)
            => checked((ushort)ParseUInt64(body, ushort.MaxValue));

        public static int ParseInt32(ReadOnlySpan<char> body)
            => checked((int)ParseInt64(body, int.MinValue, int.MaxValue));

        public static uint ParseUInt32(ReadOnlySpan<char> body)
            => checked((uint)ParseUInt64(body, uint.MaxValue));

        public static long ParseInt64(ReadOnlySpan<char> body)
            => ParseInt64(body, long.MinValue, long.MaxValue);

        public static ulong ParseUInt64(ReadOnlySpan<char> body)
            => ParseUInt64(body, ulong.MaxValue);

        private static long ParseInt64(ReadOnlySpan<char> body, long min, long max)
        {
            var trimmed = body.Trim();
            if (trimmed.IsEmpty)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            var index = 0;
            var negative = false;
            var c0 = trimmed[0];
            if (c0 == '+')
            {
                index++;
            }
            else if (c0 == '-')
            {
                negative = true;
                index++;
            }

            if (index >= trimmed.Length)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            ulong magnitude = 0;
            var anyDigit = false;
            for (; index < trimmed.Length; index++)
            {
                var c = trimmed[index];
                if (c < '0' || c > '9')
                {
                    throw new FormatException("The input is not a valid integer lexical form.");
                }

                anyDigit = true;
                var digit = (uint)(c - '0');
                if (magnitude > (ulong.MaxValue - digit) / 10)
                {
                    throw new OverflowException("Value was either too large or too small for the integer type.");
                }

                magnitude = (magnitude * 10) + digit;
            }

            if (!anyDigit)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            if (!negative)
            {
                if (magnitude > (ulong)max)
                {
                    throw new OverflowException("Value was either too large or too small for the integer type.");
                }

                return (long)magnitude;
            }

            if (min >= 0)
            {
                throw new OverflowException("Value was either too large or too small for the integer type.");
            }

            var absMin = unchecked((ulong)(-(min + 1))) + 1;
            if (magnitude > absMin)
            {
                throw new OverflowException("Value was either too large or too small for the integer type.");
            }

            if (magnitude == absMin)
            {
                return min;
            }

            return -(long)magnitude;
        }

        private static ulong ParseUInt64(ReadOnlySpan<char> body, ulong max)
        {
            var trimmed = body.Trim();
            if (trimmed.IsEmpty)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            var index = 0;
            if (trimmed[0] == '+')
            {
                index++;
            }
            else if (trimmed[0] == '-')
            {
                //как uint.Parse("-0"): минус допустим только если величина 0, иначе Overflow
                index++;
                if (index >= trimmed.Length)
                {
                    throw new FormatException("The input is not a valid integer lexical form.");
                }

                var sawDigit = false;
                for (; index < trimmed.Length; index++)
                {
                    var c = trimmed[index];
                    if (c < '0' || c > '9')
                    {
                        throw new FormatException("The input is not a valid integer lexical form.");
                    }

                    sawDigit = true;
                    if (c != '0')
                    {
                        throw new OverflowException("Value was either too large or too small for the integer type.");
                    }
                }

                if (!sawDigit)
                {
                    throw new FormatException("The input is not a valid integer lexical form.");
                }

                return 0;
            }

            if (index >= trimmed.Length)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            ulong magnitude = 0;
            var anyDigit = false;
            for (; index < trimmed.Length; index++)
            {
                var c = trimmed[index];
                if (c < '0' || c > '9')
                {
                    throw new FormatException("The input is not a valid integer lexical form.");
                }

                anyDigit = true;
                var digit = (uint)(c - '0');
                if (magnitude > (ulong.MaxValue - digit) / 10)
                {
                    throw new OverflowException("Value was either too large or too small for the integer type.");
                }

                magnitude = (magnitude * 10) + digit;
            }

            if (!anyDigit)
            {
                throw new FormatException("The input is not a valid integer lexical form.");
            }

            if (magnitude > max)
            {
                throw new OverflowException("Value was either too large or too small for the integer type.");
            }

            return magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (ToAsciiUpper(left[i]) != ToAsciiUpper(right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char ToAsciiUpper(char c)
            => (c >= 'a' && c <= 'z') ? (char)(c - ('a' - 'A')) : c;
    }
}
