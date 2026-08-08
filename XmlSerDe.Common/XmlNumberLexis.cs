using System;
using System.Globalization;
using System.Runtime.CompilerServices;

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

            var negative = value < TimeSpan.Zero;
            //Negate() на TimeSpan.MinValue бросает, поэтому знак снимается на тиках
            var ticks = negative ? -value.Ticks : value.Ticks;

            var days = ticks / TimeSpan.TicksPerDay;
            var rest = ticks % TimeSpan.TicksPerDay;
            var hours = rest / TimeSpan.TicksPerHour;
            rest %= TimeSpan.TicksPerHour;
            var minutes = rest / TimeSpan.TicksPerMinute;
            rest %= TimeSpan.TicksPerMinute;
            var seconds = rest / TimeSpan.TicksPerSecond;
            var fraction = rest % TimeSpan.TicksPerSecond;

            var sb = new System.Text.StringBuilder(24);
            if (negative)
            {
                sb.Append('-');
            }
            sb.Append('P');
            if (days != 0)
            {
                sb.Append(days.ToString(CultureInfo.InvariantCulture)).Append('D');
            }

            if (hours != 0 || minutes != 0 || seconds != 0 || fraction != 0)
            {
                sb.Append('T');
                if (hours != 0)
                {
                    sb.Append(hours.ToString(CultureInfo.InvariantCulture)).Append('H');
                }
                if (minutes != 0)
                {
                    sb.Append(minutes.ToString(CultureInfo.InvariantCulture)).Append('M');
                }
                if (seconds != 0 || fraction != 0)
                {
                    sb.Append(seconds.ToString(CultureInfo.InvariantCulture));
                    if (fraction != 0)
                    {
                        sb.Append('.').Append(fraction.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0'));
                    }
                    sb.Append('S');
                }
            }

            return sb.ToString();
        }
    }
}
