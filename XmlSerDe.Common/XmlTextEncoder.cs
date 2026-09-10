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

            var first = IndexOfEncodable(value.AsSpan());
            if (first < 0)
            {
                return value;
            }

            var sb = new StringBuilder(value.Length + 16);
            var dest = new StringBuilderDestination(sb);
            EncodeToCore(value, first, ref dest);
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

            EncodeToCore(value, IndexOfEncodable(value.AsSpan()), ref dest);
        }

        /// <summary>
        /// Текст пишется сегментами: от одного кодируемого символа до следующего
        /// идёт один <see cref="IXmlEncodeDestination.WriteSlice"/>, а сам следующий
        /// ищется векторно. Посимвольный обход после первого попадания стоил
        /// на строке из 200 латинских символов втрое дороже чистого ASCII.
        /// </summary>
        private static void EncodeToCore<T>(string value, int first, ref T dest)
            where T : struct, IXmlEncodeDestination
        {
            if (first < 0)
            {
                dest.WriteSlice(value, 0, value.Length);
                return;
            }

            var start = 0;
            var i = first;
            while (true)
            {
                if (i > start)
                {
                    dest.WriteSlice(value, start, i - start);
                }

                var ch = value[i];
                var named = NamedReplacementOrNull(ch);
                if (named is not null)
                {
                    dest.WriteLiteral(named);
                    start = i + 1;
                }
                else if (ch >= (char)160 && ch <= (char)255)
                {
                    WriteNumericEntity(ref dest, ch);
                    start = i + 1;
                }
                else if ((uint)(i + 1) < (uint)value.Length
                    && char.IsHighSurrogate(ch)
                    && char.IsLowSurrogate(value[i + 1]))
                {
                    WriteNumericEntity(ref dest, char.ConvertToUtf32(ch, value[i + 1]));
                    start = i + 2;
                }
                else
                {
                    //как HtmlEncode: непарный суррогат становится U+FFFD
                    dest.WriteLiteral("�");
                    start = i + 1;
                }

                if (start >= value.Length)
                {
                    return;
                }

                var next = IndexOfEncodableAfterHit(value.AsSpan(start));
                if (next < 0)
                {
                    dest.WriteSlice(value, start, value.Length - start);
                    return;
                }

                i = start + next;
            }
        }

        /// <summary>
        /// На сколько символов закодированный текст длиннее исходного. Точно, а не
        /// сверху: оценщик длины пересчитывает по этому числу буфер второго прохода,
        /// и занижение там оборачивается ростом буфера с копированием, а завышение -
        /// лишней арендой.
        /// </summary>
        public static int EncodedOverhead(ReadOnlySpan<char> value)
        {
            //первое попадание ищется векторно - на обычной строке его нет, и это
            //весь подсчёт; дальше тесный посимвольный цикл: кодируемые символы
            //ходят кучно, и повторный поиск на каждое попадание стоил дороже
            var first = IndexOfEncodable(value);
            if (first < 0)
            {
                return 0;
            }

            var overhead = 0;
            var table = OverheadLatin1;
            for (var i = first; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch < (char)256)
                {
                    //таблица вместо переключателя и сравнений: на плотной латинице
                    //это половина стоимости подсчёта
                    overhead += table[ch];
                }
                else if (char.IsSurrogate(ch))
                {
                    if ((uint)(i + 1) < (uint)value.Length
                        && char.IsHighSurrogate(ch)
                        && char.IsLowSurrogate(value[i + 1]))
                    {
                        //&#N; c N от 65536 (5 цифр) до 1114111 (7 цифр) на месте двух символов
                        var codePoint = char.ConvertToUtf32(ch, value[i + 1]);
                        var digits = codePoint >= 1_000_000 ? 7 : codePoint >= 100_000 ? 6 : 5;
                        overhead += 2 + digits + 1 - 2;
                        i++;
                    }
                    //непарный суррогат становится U+FFFD - тот же один символ
                }
            }

            return overhead;
        }

        /// <summary>
        /// На сколько удлиняется каждый символ Latin-1 при кодировании: 0 для
        /// обычного, длина замены минус один для пяти именованных, 5 для 160..255
        /// (<c>&amp;#NNN;</c> - шесть символов на месте одного).
        /// </summary>
        private static readonly byte[] OverheadLatin1 = BuildOverheadLatin1();

        private static byte[] BuildOverheadLatin1()
        {
            var table = new byte[256];
            for (var c = 0; c < 256; c++)
            {
                var named = NamedReplacementOrNull((char)c);
                if (named is not null)
                {
                    table[c] = (byte)(named.Length - 1);
                }
                else if (c >= 160)
                {
                    table[c] = 5;
                }
            }

            return table;
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Всё кодируемое, что лежит в Latin-1: пять именованных замен и 160..255.
        /// Суррогаты ищутся отдельным <c>IndexOfAnyInRange</c>: набор из 2048 значений
        /// выше 255 увёл бы <see cref="System.Buffers.SearchValues"/> в вероятностную
        /// карту, где совпадает каждый младший байт, и поиск выродился бы в посимвольный.
        /// </summary>
        private static readonly System.Buffers.SearchValues<char> EncodableLatin1 =
            System.Buffers.SearchValues.Create(BuildEncodableLatin1());

        private static string BuildEncodableLatin1()
        {
            var sb = new StringBuilder("<>\"'&");
            for (var c = 160; c <= 255; c++)
            {
                sb.Append((char)c);
            }

            return sb.ToString();
        }
#endif

        /// <summary>
        /// Сколько символов после попадания проверяется по одному, прежде чем
        /// снова звать векторный поиск. Кодируемые символы ходят кучно - латиница
        /// подряд, спецсимволы через два-три знака, - и на такой плотности
        /// векторный вызов на каждое попадание проигрывал посимвольному циклу
        /// втрое; на редких попаданиях окно стоит несколько сравнений.
        /// </summary>
        private const int ScalarWindow = 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEncodable(char ch)
        {
            return NamedReplacementOrNull(ch) is not null
                || (ch >= (char)160 && ch <= (char)255)
                || char.IsSurrogate(ch);
        }

        private static int IndexOfEncodableAfterHit(ReadOnlySpan<char> value)
        {
#if NET8_0_OR_GREATER
            var window = Math.Min(ScalarWindow, value.Length);
            for (var i = 0; i < window; i++)
            {
                if (IsEncodable(value[i]))
                {
                    return i;
                }
            }

            if (window == value.Length)
            {
                return -1;
            }

            var tail = IndexOfEncodable(value.Slice(window));
            return tail < 0 ? -1 : window + tail;
#else
            return IndexOfEncodable(value);
#endif
        }

        private static int IndexOfEncodable(ReadOnlySpan<char> value)
        {
#if NET8_0_OR_GREATER
            var latin1 = value.IndexOfAny(EncodableLatin1);
            var prefix = latin1 < 0 ? value : value.Slice(0, latin1);
            var surrogate = prefix.IndexOfAnyInRange('\uD800', '\uDFFF');
            return surrogate >= 0 ? surrogate : latin1;
#else
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
#endif
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
