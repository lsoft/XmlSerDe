using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// Exhauster that writes into StringBuilder.
    /// Class is NOT a thread-safe!
    /// </summary>
    public class StringBuilderExhauster : IExhauster
    {
        private readonly StringBuilder _sb;
        private readonly string _dateTimeFormat;

        /// <param name="dateTimeFormat">
        /// По умолчанию - лексическая форма, которую даёт
        /// <c>XmlConvert.ToString(value, XmlDateTimeSerializationMode.RoundtripKind)</c>,
        /// то есть та же, что пишет System.Xml.Serialization.
        ///
        /// Ключ здесь - <c>FFFFFFF</c> вместо <c>fffffff</c>: незначащие нули дробной
        /// части не пишутся, а когда она нулевая целиком - вместе с ней пропадает и
        /// сама точка. Прежнее <c>fffffff</c> давало на ровной секунде
        /// <c>...T14:30:45.0000000Z</c> там, где BCL пишет <c>...T14:30:45Z</c>;
        /// оба варианта разбираются одинаково, но документы посимвольно не совпадали.
        /// </param>
        public StringBuilderExhauster(
            StringBuilder? sb = null,
            string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
            )
        {
            if (dateTimeFormat is null)
            {
                throw new ArgumentNullException(nameof(dateTimeFormat));
            }

            _sb = sb ?? new StringBuilder();
            _dateTimeFormat = dateTimeFormat;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _sb.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return _sb.ToString();
        }

        /// <summary>
        /// Formats a value using an XSD-compatible, culture-invariant lexical
        /// representation (decimal point, ASCII digits) regardless of the
        /// current thread's culture.
        /// </summary>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendInvariant<T>(T value) where T : ISpanFormattable
        {
            Span<char> buffer = stackalloc char[40];
            if (value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
            {
                _sb.Append(buffer.Slice(0, written));
            }
            else
            {
                _sb.Append(value.ToString(null, CultureInfo.InvariantCulture));
            }
        }
#else
        /// <remarks>
        /// netstandard2.0 не знает ни ISpanFormattable, ни StringBuilder.Append(roschar),
        /// поэтому здесь остаётся форматирование через промежуточную строку.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendInvariant<T>(T value) where T : IFormattable
        {
            _sb.Append(value.ToString(null, CultureInfo.InvariantCulture));
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
#if NET8_0_OR_GREATER
            Span<char> buffer = stackalloc char[64];
            if (value.TryFormat(buffer, out var written, _dateTimeFormat.AsSpan(), CultureInfo.InvariantCulture))
            {
                _sb.Append(buffer.Slice(0, written));
            }
            else
            {
                _sb.Append(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture));
            }
#else
            _sb.Append(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid value)
        {
#if NET8_0_OR_GREATER
            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out var written))
            {
                _sb.Append(buffer.Slice(0, written));
            }
            else
            {
                _sb.Append(value.ToString());
            }
#else
            _sb.Append(value.ToString());
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool value)
        {
            _sb.Append((value ? "true" : "false"));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _sb.Append((value.Value ? "true" : "false"));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value)
        {
            AppendInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        /// <summary>
        /// Формат "R", а не форматирование по умолчанию: на netstandard2.0 и net472
        /// <c>ToString()</c> отдаёт 15 значащих цифр и теряет точность, и round-trip
        /// не замкнулся бы. Начиная с .NET Core 3.0 "R" уже даёт кратчайшее
        /// round-trippable представление - то же, что <c>XmlConvert.ToString</c>.
        ///
        /// Бесконечности и NaN пишутся руками: у <c>double.ToString</c> это
        /// "Infinity"/"-Infinity", а в xsd - "INF"/"-INF", и BCL пишет именно "INF".
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                _sb.Append(special);
                return;
            }

            AppendRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(float value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                _sb.Append(special);
                return;
            }

            AppendRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(float? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendRoundtrip<T>(T value) where T : ISpanFormattable
        {
            Span<char> buffer = stackalloc char[40];
            if (value.TryFormat(buffer, out var written, "R".AsSpan(), CultureInfo.InvariantCulture))
            {
                _sb.Append(buffer.Slice(0, written));
            }
            else
            {
                _sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendRoundtrip<T>(T value) where T : IFormattable
        {
            _sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char value)
        {
            //кодовая точка, а не символ - см. IExhauster
            AppendInvariant((ushort)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan value)
        {
            XmlNumberLexis.Append(_sb, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            XmlCharGuard.EnsureValidXmlChars(value.AsSpan());
            XmlTextEncoder.Append(_sb, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncodedUnchecked(string? value)
        {
            if (value is null)
            {
                return;
            }

            XmlTextEncoder.Append(_sb, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendAttributeEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            XmlAttributeEncoder.AppendChecked(_sb, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendAttributeEncodedUnchecked(string? value)
        {
            if (value is null)
            {
                return;
            }

            XmlAttributeEncoder.Append(_sb, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendBase64(byte[]? value)
        {
            if (value is null)
            {
                return;
            }

            XmlBase64.Append(_sb, value);
        }
    }
}
