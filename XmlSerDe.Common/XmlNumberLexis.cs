using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Лексические формы, в которых <c>System.Xml.Serialization</c> пишет
    /// вещественные числа и <see cref="TimeSpan"/>, - в одном месте, потому что
    /// их обязаны знать одинаково и три экзостера, и инжектор, а разойдясь, они
    /// разошлись бы молча.
    ///
    /// Формы сняты прогоном самого <c>XmlSerializer</c>, а не выведены:
    /// <list type="bullet">
    /// <item>вещественные - кратчайшее round-trippable представление, но
    /// бесконечности пишутся как <c>INF</c>/<c>-INF</c>, а не "Infinity";</item>
    /// <item><see cref="TimeSpan"/> - длительность ISO-8601 (<c>P1DT2H3M4.005S</c>),
    /// у нуля - <c>PT0S</c>, у отрицательной - ведущий минус.</item>
    /// </list>
    /// </summary>
    public static class XmlNumberLexis
    {
        public const string NaN = "NaN";
        public const string PositiveInfinity = "INF";
        public const string NegativeInfinity = "-INF";

        /// <summary>
        /// Лексема для значения, у которого её нельзя получить форматированием, -
        /// или null, если значение обычное и его надо форматировать как число.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? SpecialOrNull(double value)
        {
            if (double.IsNaN(value))
            {
                return NaN;
            }
            if (double.IsPositiveInfinity(value))
            {
                return PositiveInfinity;
            }
            if (double.IsNegativeInfinity(value))
            {
                return NegativeInfinity;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? SpecialOrNull(float value)
        {
            if (float.IsNaN(value))
            {
                return NaN;
            }
            if (float.IsPositiveInfinity(value))
            {
                return PositiveInfinity;
            }
            if (float.IsNegativeInfinity(value))
            {
                return NegativeInfinity;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ParseDouble(ReadOnlySpan<char> body)
        {
            var special = TryParseSpecial(body);
            if (special.HasValue)
            {
                return special.Value;
            }

#if NET8_0_OR_GREATER
            return double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
#else
            return double.Parse(body.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ParseSingle(ReadOnlySpan<char> body)
        {
            var special = TryParseSpecial(body);
            if (special.HasValue)
            {
                return (float)special.Value;
            }

#if NET8_0_OR_GREATER
            return float.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
#else
            return float.Parse(body.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
#endif
        }

        /// <summary>
        /// Лексемы xsd сверяются с учётом регистра: по спецификации это литералы,
        /// а не имена. Разбор обычного числа их всё равно не принял бы, поэтому
        /// проверка стоит перед ним, а не после.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double? TryParseSpecial(ReadOnlySpan<char> body)
        {
            var trimmed = body.Trim();

            if (trimmed.SequenceEqual(NaN.AsSpan()))
            {
                return double.NaN;
            }
            if (trimmed.SequenceEqual(PositiveInfinity.AsSpan()))
            {
                return double.PositiveInfinity;
            }
            if (trimmed.SequenceEqual(NegativeInfinity.AsSpan()))
            {
                return double.NegativeInfinity;
            }

            return null;
        }

        /// <summary>
        /// Длительность ISO-8601. Дробная часть секунд пишется только когда она
        /// есть, и без хвостовых нулей - как у <c>XmlConvert.ToString(TimeSpan)</c>.
        /// </summary>
        public static string ToDurationString(TimeSpan value)
        {
            if (value == TimeSpan.Zero)
            {
                return "PT0S";
            }

            Span<char> buffer = stackalloc char[32];
            var written = WriteDuration(buffer, value);
#if NET8_0_OR_GREATER
            return new string(buffer.Slice(0, written));
#else
            return buffer.Slice(0, written).ToString();
#endif
        }

        public static void Append(StringBuilder sb, TimeSpan value)
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            Span<char> buffer = stackalloc char[32];
            var written = WriteDuration(buffer, value);
            AppendSpan(sb, buffer.Slice(0, written));
        }

        public static bool TryFormat(Span<char> destination, TimeSpan value, out int charsWritten)
        {
            var needed = DurationCharCount(value);
            if (destination.Length < needed)
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = WriteDuration(destination, value);
            return true;
        }

        /// <summary>
        /// Длина <see cref="ToDurationString"/> без построения строки.
        /// <c>TimeSpan.MinValue</c> - 27 символов (<c>-P10675199DT2H48M5.4775808S</c>).
        /// </summary>
        public static int DurationCharCount(TimeSpan value)
        {
            if (value == TimeSpan.Zero)
            {
                return 4;
            }

            SplitDuration(value, out var negative, out var days, out var hours, out var minutes, out var seconds, out var fraction);
            return DurationCharCount(negative, days, hours, minutes, seconds, fraction);
        }

        private static void SplitDuration(
            TimeSpan value,
            out bool negative,
            out ulong days,
            out ulong hours,
            out ulong minutes,
            out ulong seconds,
            out ulong fraction)
        {
            negative = value < TimeSpan.Zero;
            // TimeSpan.MinValue.Ticks не помещается в long после смены знака:
            // модуль берётся как ulong, тот же трюк, что для int.MinValue.
            var ticks = negative ? 0ul - (ulong)value.Ticks : (ulong)value.Ticks;

            days = ticks / (ulong)TimeSpan.TicksPerDay;
            var rest = ticks % (ulong)TimeSpan.TicksPerDay;
            hours = rest / (ulong)TimeSpan.TicksPerHour;
            rest %= (ulong)TimeSpan.TicksPerHour;
            minutes = rest / (ulong)TimeSpan.TicksPerMinute;
            rest %= (ulong)TimeSpan.TicksPerMinute;
            seconds = rest / (ulong)TimeSpan.TicksPerSecond;
            fraction = rest % (ulong)TimeSpan.TicksPerSecond;
        }

        private static int DurationCharCount(
            bool negative,
            ulong days,
            ulong hours,
            ulong minutes,
            ulong seconds,
            ulong fraction)
        {
            var n = negative ? 2 : 1; // '-' + 'P' или только 'P'
            if (days != 0)
            {
                n += CountDigits(days) + 1;
            }

            if (hours != 0 || minutes != 0 || seconds != 0 || fraction != 0)
            {
                n += 1;
                if (hours != 0)
                {
                    n += CountDigits(hours) + 1;
                }
                if (minutes != 0)
                {
                    n += CountDigits(minutes) + 1;
                }
                if (seconds != 0 || fraction != 0)
                {
                    n += CountDigits(seconds);
                    if (fraction != 0)
                    {
                        n += 1 + CountTrimmedFractionDigits(fraction);
                    }
                    n += 1;
                }
            }

            return n;
        }

        /// <summary>
        /// Дни длительности не длиннее 8 цифр (<c>10675199</c>).
        /// </summary>
        private static int CountDigits(ulong value)
        {
            if (value < 10) return 1;
            if (value < 100) return 2;
            if (value < 1000) return 3;
            if (value < 10000) return 4;
            if (value < 100000) return 5;
            if (value < 1000000) return 6;
            if (value < 10000000) return 7;
            return 8;
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

        /// <summary>
        /// Потолок формы - 27 символов (<see cref="TimeSpan.MinValue"/>).
        /// </summary>
        private static int WriteDuration(Span<char> dest, TimeSpan value)
        {
            if (value == TimeSpan.Zero)
            {
                dest[0] = 'P';
                dest[1] = 'T';
                dest[2] = '0';
                dest[3] = 'S';
                return 4;
            }

            SplitDuration(value, out var negative, out var days, out var hours, out var minutes, out var seconds, out var fraction);

            var n = 0;
            if (negative)
            {
                dest[n++] = '-';
            }

            dest[n++] = 'P';
            if (days != 0)
            {
                n += WriteUInt(dest, n, days);
                dest[n++] = 'D';
            }

            if (hours != 0 || minutes != 0 || seconds != 0 || fraction != 0)
            {
                dest[n++] = 'T';
                if (hours != 0)
                {
                    n += WriteUInt(dest, n, hours);
                    dest[n++] = 'H';
                }
                if (minutes != 0)
                {
                    n += WriteUInt(dest, n, minutes);
                    dest[n++] = 'M';
                }
                if (seconds != 0 || fraction != 0)
                {
                    n += WriteUInt(dest, n, seconds);
                    if (fraction != 0)
                    {
                        dest[n++] = '.';
                        n += WriteFraction(dest, n, fraction);
                    }
                    dest[n++] = 'S';
                }
            }

            return n;
        }

        private static int WriteUInt(Span<char> dest, int offset, ulong value)
        {
            var digits = CountDigits(value);
            var i = offset + digits;
            do
            {
                i--;
                dest[i] = (char)('0' + (int)(value % 10));
                value /= 10;
            }
            while (i > offset);

            return digits;
        }

        private static int WriteFraction(Span<char> dest, int offset, ulong fraction)
        {
            Span<char> padded = stackalloc char[7];
            var rest = fraction;
            for (var i = 6; i >= 0; i--)
            {
                padded[i] = (char)('0' + (int)(rest % 10));
                rest /= 10;
            }

            var keep = CountTrimmedFractionDigits(fraction);
            padded.Slice(0, keep).CopyTo(dest.Slice(offset));
            return keep;
        }

        private static void AppendSpan(StringBuilder sb, ReadOnlySpan<char> chars)
        {
#if NET8_0_OR_GREATER
            sb.Append(chars);
#else
            for (var i = 0; i < chars.Length; i++)
            {
                sb.Append(chars[i]);
            }
#endif
        }

        /// <summary>
        /// Разбор xsd:duration, тот же, что <see cref="System.Xml.XmlConvert.ToTimeSpan(string)"/>:
        /// год = 365 дней, месяц = 30. Без промежуточной строки.
        /// </summary>
        public static TimeSpan ParseDuration(ReadOnlySpan<char> body)
        {
            var trimmed = body.Trim();
            if (!TryParseDuration(trimmed, out var value, out var overflow))
            {
                throw overflow
                    ? new OverflowException("Duration overflowed TimeSpan.")
                    : new FormatException("The input is not a valid xsd:duration lexical form.");
            }

            return value;
        }

        private static bool TryParseDuration(ReadOnlySpan<char> s, out TimeSpan value, out bool overflow)
        {
            value = default;
            overflow = false;

            if (s.IsEmpty)
            {
                return false;
            }

            var pos = 0;
            var negative = false;
            if (s[pos] == '-')
            {
                negative = true;
                pos++;
                if (pos >= s.Length)
                {
                    return false;
                }
            }

            if (s[pos++] != 'P')
            {
                return false;
            }

            var years = 0;
            var months = 0;
            var days = 0;
            var hours = 0;
            var minutes = 0;
            var seconds = 0;
            var nanoseconds = 0;
            var parts = 0;

            if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out var number, out var digits))
            {
                overflow = true;
                return false;
            }

            if (pos >= s.Length)
            {
                return false;
            }

            if (s[pos] == 'Y')
            {
                if (digits == 0)
                {
                    return false;
                }

                parts |= 1;
                years = number;
                pos++;
                if (pos == s.Length)
                {
                    goto Convert;
                }

                if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out number, out digits))
                {
                    overflow = true;
                    return false;
                }

                if (pos >= s.Length)
                {
                    return false;
                }
            }

            if (s[pos] == 'M')
            {
                if (digits == 0)
                {
                    return false;
                }

                parts |= 2;
                months = number;
                pos++;
                if (pos == s.Length)
                {
                    goto Convert;
                }

                if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out number, out digits))
                {
                    overflow = true;
                    return false;
                }

                if (pos >= s.Length)
                {
                    return false;
                }
            }

            if (s[pos] == 'D')
            {
                if (digits == 0)
                {
                    return false;
                }

                parts |= 4;
                days = number;
                pos++;
                if (pos == s.Length)
                {
                    goto Convert;
                }

                if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out _, out digits))
                {
                    overflow = true;
                    return false;
                }

                if (pos >= s.Length)
                {
                    return false;
                }
            }

            if (s[pos] == 'T')
            {
                if (digits != 0)
                {
                    return false;
                }

                pos++;
                if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out number, out digits))
                {
                    overflow = true;
                    return false;
                }

                if (pos >= s.Length)
                {
                    return false;
                }

                if (s[pos] == 'H')
                {
                    if (digits == 0)
                    {
                        return false;
                    }

                    parts |= 8;
                    hours = number;
                    pos++;
                    if (pos == s.Length)
                    {
                        goto Convert;
                    }

                    if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out number, out digits))
                    {
                        overflow = true;
                        return false;
                    }

                    if (pos >= s.Length)
                    {
                        return false;
                    }
                }

                if (s[pos] == 'M')
                {
                    if (digits == 0)
                    {
                        return false;
                    }

                    parts |= 16;
                    minutes = number;
                    pos++;
                    if (pos == s.Length)
                    {
                        goto Convert;
                    }

                    if (!TryParseDurationDigits(s, ref pos, eatExtra: false, out number, out digits))
                    {
                        overflow = true;
                        return false;
                    }

                    if (pos >= s.Length)
                    {
                        return false;
                    }
                }

                if (s[pos] == '.')
                {
                    pos++;
                    parts |= 32;
                    seconds = number;

                    if (!TryParseDurationDigits(s, ref pos, eatExtra: true, out number, out digits))
                    {
                        overflow = true;
                        return false;
                    }

                    if (digits == 0)
                    {
                        number = 0;
                    }

                    while (digits > 9)
                    {
                        number /= 10;
                        digits--;
                    }

                    while (digits < 9)
                    {
                        number *= 10;
                        digits++;
                    }

                    nanoseconds = number;
                    if (pos >= s.Length || s[pos] != 'S')
                    {
                        return false;
                    }

                    pos++;
                    if (pos == s.Length)
                    {
                        goto Convert;
                    }
                }
                else if (s[pos] == 'S')
                {
                    if (digits == 0)
                    {
                        return false;
                    }

                    parts |= 32;
                    seconds = number;
                    pos++;
                    if (pos == s.Length)
                    {
                        goto Convert;
                    }
                }
            }

            if (digits != 0 || pos != s.Length)
            {
                return false;
            }

        Convert:
            if (parts == 0)
            {
                return false;
            }

            try
            {
                checked
                {
                    ulong ticks = 0;
                    ticks += ((ulong)years + (ulong)months / 12) * 365;
                    ticks += ((ulong)months % 12) * 30;
                    ticks += (ulong)days;
                    ticks *= 24;
                    ticks += (ulong)hours;
                    ticks *= 60;
                    ticks += (ulong)minutes;
                    ticks *= 60;
                    ticks += (ulong)seconds;
                    ticks *= (ulong)TimeSpan.TicksPerSecond;
                    ticks += (ulong)nanoseconds / 100;

                    if (negative)
                    {
                        if (ticks == (ulong)long.MaxValue + 1)
                        {
                            value = TimeSpan.MinValue;
                            return true;
                        }

                        value = new TimeSpan(-((long)ticks));
                    }
                    else
                    {
                        value = new TimeSpan((long)ticks);
                    }

                    return true;
                }
            }
            catch (OverflowException)
            {
                overflow = true;
                return false;
            }
        }

        private static bool TryParseDurationDigits(
            ReadOnlySpan<char> s,
            ref int offset,
            bool eatExtra,
            out int result,
            out int numDigits
            )
        {
            var start = offset;
            result = 0;
            numDigits = 0;

            while (offset < s.Length)
            {
                var c = s[offset];
                if (c < '0' || c > '9')
                {
                    break;
                }

                var digit = c - '0';
                if (result > (int.MaxValue - digit) / 10)
                {
                    if (!eatExtra)
                    {
                        return false;
                    }

                    numDigits = offset - start;
                    while (offset < s.Length && s[offset] >= '0' && s[offset] <= '9')
                    {
                        offset++;
                    }

                    return true;
                }

                result = (result * 10) + digit;
                offset++;
            }

            numDigits = offset - start;
            return true;
        }
    }
}
