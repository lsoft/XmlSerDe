using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Экранирование текста тела элемента. Лексическая форма совпадает с
    /// <c>WebUtility.HtmlEncode</c>: <c>&lt; &gt; &amp; &quot;</c>, апостроф как
    /// <c>&amp;#39;</c>, латиница 160–255 и скаляры вне BMP как <c>&amp;#N;</c>.
    /// Пишет сразу в сток, без промежуточной строки.
    /// </summary>
    public static class XmlTextEncoder
    {
        public static string Encode(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (IndexOfEncodable(value.AsSpan()) < 0)
            {
                return value;
            }

            var sb = new StringBuilder(value.Length);
            Append(sb, value);
            return sb.ToString();
        }

        public static void Append(StringBuilder sb, string value)
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var dest = new StringBuilderDestination(sb);
            EncodeTo(value, ref dest);
        }

        public static void EncodeTo<T>(string value, ref T dest)
            where T : struct, IXmlEncodeDestination
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var first = IndexOfEncodable(value.AsSpan());
            if (first < 0)
            {
                dest.WriteSlice(value, 0, value.Length);
                return;
            }

            var start = 0;
            for (var i = first; i < value.Length; i++)
            {
                var ch = value[i];
                var named = NamedReplacementOrNull(ch);
                if (named is not null)
                {
                    if (i > start)
                    {
                        dest.WriteSlice(value, start, i - start);
                    }

                    dest.WriteLiteral(named);
                    start = i + 1;
                    continue;
                }

                if (ch >= (char)160 && ch <= (char)255)
                {
                    if (i > start)
                    {
                        dest.WriteSlice(value, start, i - start);
                    }

                    WriteNumericEntity(ref dest, ch);
                    start = i + 1;
                    continue;
                }

                if (!char.IsSurrogate(ch))
                {
                    continue;
                }

                if (i > start)
                {
                    dest.WriteSlice(value, start, i - start);
                }

                if ((uint)(i + 1) < (uint)value.Length
                    && char.IsHighSurrogate(ch)
                    && char.IsLowSurrogate(value[i + 1]))
                {
                    WriteNumericEntity(ref dest, char.ConvertToUtf32(ch, value[i + 1]));
                    i++;
                }
                else
                {
                    //как HtmlEncode: непарный суррогат становится U+FFFD
                    dest.WriteLiteral("\uFFFD");
                }

                start = i + 1;
            }

            if (start < value.Length)
            {
                dest.WriteSlice(value, start, value.Length - start);
            }
        }

        private static int IndexOfEncodable(ReadOnlySpan<char> value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (NamedReplacementOrNull(ch) is not null
                    || (ch >= (char)160 && ch <= (char)255)
                    || char.IsSurrogate(ch))
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? NamedReplacementOrNull(char c)
        {
            switch (c)
            {
                case '<':
                    return "&lt;";
                case '>':
                    return "&gt;";
                case '"':
                    return "&quot;";
                case '\'':
                    return "&#39;";
                case '&':
                    return "&amp;";
                default:
                    return null;
            }
        }

        private static void WriteNumericEntity<T>(ref T dest, int codePoint)
            where T : struct, IXmlEncodeDestination
        {
            Span<char> buffer = stackalloc char[12];
            var written = FormatNumericEntity(buffer, codePoint);
            dest.WriteChars(buffer.Slice(0, written));
        }

        /// <summary>
        /// <see cref="Encode"/> plus <see cref="XmlCharGuard"/> (opt-in
        /// <see cref="XmlFeature.CharGuard"/>).
        /// </summary>
        public static string EncodeChecked(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            XmlCharGuard.EnsureValidXmlChars(value.AsSpan());
            return Encode(value);
        }

        public static void AppendChecked(StringBuilder sb, string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            XmlCharGuard.EnsureValidXmlChars(value.AsSpan());
            Append(sb, value);
        }

        private static int FormatNumericEntity(Span<char> dest, int codePoint)
        {
            dest[0] = '&';
            dest[1] = '#';

            var u = (uint)codePoint;
            var digits = 1;
            var tmp = u;
            while (tmp >= 10)
            {
                tmp /= 10;
                digits++;
            }

            var semicolon = 2 + digits;
            dest[semicolon] = ';';
            var i = semicolon;
            do
            {
                i--;
                dest[i] = (char)('0' + (u % 10));
                u /= 10;
            }
            while (i > 2);

            return semicolon + 1;
        }

        private readonly struct StringBuilderDestination : IXmlEncodeDestination
        {
            private readonly StringBuilder _sb;

            public StringBuilderDestination(StringBuilder sb)
            {
                _sb = sb;
            }

            public void WriteSlice(string value, int start, int count)
            {
                if (count == 0)
                {
                    return;
                }

                _sb.Append(value, start, count);
            }

            public void WriteLiteral(string literal)
            {
                _sb.Append(literal);
            }

            public void WriteChars(ReadOnlySpan<char> chars)
            {
#if NET8_0_OR_GREATER
                _sb.Append(chars);
#else
                for (var i = 0; i < chars.Length; i++)
                {
                    _sb.Append(chars[i]);
                }
#endif
            }
        }
    }
}
