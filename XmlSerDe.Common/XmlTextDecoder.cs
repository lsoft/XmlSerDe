using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Раскрытие ссылок (XML 1.0 §4.1) и секций CDATA (§2.7) прямо из спана.
    /// На выходе ровно одна аллокация - сам результат; промежуточных строк нет.
    ///
    /// Заменяет собой WebUtility.HtmlDecode, который стоял здесь раньше и
    /// требовал материализовать вход в строку только затем, чтобы отдать её
    /// декодеру. Помимо лишней аллокации это было ещё и неверно по существу:
    /// HtmlDecode знает несколько сотен HTML-сущностей (&amp;nbsp;, &amp;copy;, …),
    /// которых в XML без DTD не существует, и молча проглатывает ссылку на
    /// необъявленную сущность - а это нарушение well-formedness (§4.1, WFC:
    /// Entity Declared), то есть фатальная ошибка.
    ///
    /// Здесь распознаются ровно пять предопределённых сущностей (§4.6:
    /// amp, lt, gt, apos, quot) и символьные ссылки &amp;#NN; / &amp;#xNN;.
    /// Всё остальное - исключение.
    /// </summary>
    public static class XmlTextDecoder
    {
        /// <summary>
        /// До какой длины входа буфер берётся со стека. Дальше - из пула:
        /// результат декодирования никогда не длиннее входа (и ссылка, и
        /// секция CDATA только укорачивают текст), поэтому размер входа
        /// всегда достаточен.
        /// </summary>
        private const int StackallocThreshold = 256;

        private static roschar CDataHeadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "<![CDATA[".AsSpan();
        }

        private static roschar CDataTailSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "]]>".AsSpan();
        }

        #region содержимое элемента

        /// <summary>
        /// Текст элемента: секции CDATA отдаются как есть, всё вне них
        /// раскрывается по §4.1. Пробельные символы сохраняются - нормализация
        /// §3.3.3 относится только к значениям атрибутов,
        /// см. <see cref="DecodeAttributeValue"/>.
        /// </summary>
        public static string DecodeElementText(roschar text)
        {
            if (text.IsEmpty)
            {
                return string.Empty;
            }

            //один векторизованный проход отсекает текст, которому раскрытие не
            //нужно вообще: в разметке элемента '&' и '<' могут стоять только
            //как начало ссылки и как начало CDATA соответственно
            if (text.IndexOfAny('&', '<') < 0)
            {
                return text.ToString();
            }

            if (text.Length <= StackallocThreshold)
            {
                Span<char> stackBuffer = stackalloc char[text.Length];
                var stackWritten = DecodeElementTextInto(text, stackBuffer);
                return stackBuffer.Slice(0, stackWritten).ToString();
            }

            var rented = ArrayPool<char>.Shared.Rent(text.Length);
            try
            {
                var written = DecodeElementTextInto(text, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Возвращает количество записанных символов. <paramref name="destination"/>
        /// обязан вмещать <paramref name="text"/> целиком.
        /// </summary>
        public static int DecodeElementTextInto(roschar text, Span<char> destination)
        {
            if (destination.Length < text.Length)
            {
                throw new ArgumentException(
                    "Destination buffer must be at least as long as the source text.",
                    nameof(destination)
                    );
            }

            var written = 0;
            var index = 0;

            while (index < text.Length)
            {
                var rest = text.Slice(index);

                var next = rest.IndexOfAny('&', '<');
                if (next < 0)
                {
                    rest.CopyTo(destination.Slice(written));
                    return written + rest.Length;
                }

                if (next > 0)
                {
                    rest.Slice(0, next).CopyTo(destination.Slice(written));
                    written += next;
                    index += next;
                    rest = text.Slice(index);
                }

                if (rest[0] == '&')
                {
                    index += DecodeReference(text, index, destination, ref written);
                    continue;
                }

                //'<' в содержимом элемента законен только как начало CDATA:
                //§2.4 прямо запрещает литеральный '<' в character data
                if (!rest.StartsWith(CDataHeadSpan))
                {
                    throw new InvalidOperationException(
                        $"Literal '<' at position {index} of element text is not allowed by XML 1.0 §2.4; use &lt; or a CDATA section."
                        );
                }

                var afterHead = rest.Slice(CDataHeadSpan.Length);
                var tail = afterHead.IndexOf(CDataTailSpan);
                if (tail < 0)
                {
                    throw new InvalidOperationException(
                        $"Closing ']]>' not found for the CDATA section starting at position {index}."
                        );
                }

                afterHead.Slice(0, tail).CopyTo(destination.Slice(written));
                written += tail;
                index += CDataHeadSpan.Length + tail + CDataTailSpan.Length;
            }

            return written;
        }

        #endregion

        #region значение атрибута

        /// <summary>
        /// XML 1.0 §3.3.3: (1) каждый литеральный TAB, CR или LF заменяется одним
        /// пробелом, (2) ссылки раскрываются. Порядок важен: символ, пришедший
        /// из ссылки (&amp;#10;), вставляется дословно и под замену не попадает -
        /// поэтому замена применяется только к literal-тексту источника,
        /// а результат раскрытия ссылок копируется как есть.
        ///
        /// Секции CDATA внутри значения атрибута невозможны (§2.4), поэтому
        /// здесь их нет.
        /// </summary>
        public static string DecodeAttributeValue(roschar rawValue)
        {
            if (rawValue.IsEmpty)
            {
                return string.Empty;
            }

            if (rawValue.Length <= StackallocThreshold)
            {
                Span<char> stackBuffer = stackalloc char[rawValue.Length];
                var stackWritten = DecodeAttributeValueInto(rawValue, stackBuffer);
                return stackBuffer.Slice(0, stackWritten).ToString();
            }

            var rented = ArrayPool<char>.Shared.Rent(rawValue.Length);
            try
            {
                var written = DecodeAttributeValueInto(rawValue, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Возвращает количество записанных символов. <paramref name="destination"/>
        /// обязан вмещать <paramref name="rawValue"/> целиком.
        /// </summary>
        public static int DecodeAttributeValueInto(roschar rawValue, Span<char> destination)
        {
            if (destination.Length < rawValue.Length)
            {
                throw new ArgumentException(
                    "Destination buffer must be at least as long as the source value.",
                    nameof(destination)
                    );
            }

            var written = 0;
            var index = 0;

            while (index < rawValue.Length)
            {
                var rest = rawValue.Slice(index);

                var next = rest.IndexOf('&');
                var literal = next < 0 ? rest : rest.Slice(0, next);

                CopyNormalized(literal, destination.Slice(written));
                written += literal.Length;
                index += literal.Length;

                if (next < 0)
                {
                    return written;
                }

                index += DecodeReference(rawValue, index, destination, ref written);
            }

            return written;
        }

        /// <summary>
        /// Шаг (1) нормализации: литеральные TAB/CR/LF становятся пробелами.
        /// </summary>
        private static void CopyNormalized(roschar source, Span<char> destination)
        {
            source.CopyTo(destination);

            //подавляющее большинство значений не содержит ни одного такого
            //символа, и один векторизованный проход это выясняет
            if (source.IndexOfAny('\t', '\r', '\n') < 0)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '\t' || c == '\r' || c == '\n')
                {
                    destination[i] = ' ';
                }
            }
        }

        #endregion

        #region ссылки

        /// <summary>
        /// Раскрывает одну ссылку, начинающуюся с '&amp;' в позиции
        /// <paramref name="start"/>, и возвращает её длину в символах источника.
        /// </summary>
        private static int DecodeReference(
            roschar source,
            int start,
            Span<char> destination,
            ref int written
            )
        {
            var rest = source.Slice(start);

            //искать ';' по всему остатку нельзя: на тексте вида "a & b; c"
            //это дало бы бессмысленное "имя сущности" длиной в полстроки,
            //поэтому имя ограничивается первым же символом, который в Name
            //стоять не может
            var length = 1;
            while (length < rest.Length && rest[length] != ';')
            {
                var c = rest[length];
                if (c == '&' || c == '<' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    break;
                }

                length++;
            }

            if (length >= rest.Length || rest[length] != ';')
            {
                throw new InvalidOperationException(
                    $"Unterminated entity reference at position {start}: '&' must start a reference ending with ';' (XML 1.0 §4.1); a literal ampersand must be written as &amp;amp;."
                    );
            }

            var name = rest.Slice(1, length - 1);
            if (name.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Empty entity reference '&;' at position {start} (XML 1.0 §4.1)."
                    );
            }

            if (name[0] == '#')
            {
                WriteCharacterReference(name.Slice(1), start, destination, ref written);
            }
            else
            {
                WritePredefinedEntity(name, start, destination, ref written);
            }

            //длина самой ссылки: '&' + имя + ';'
            return length + 1;
        }

        /// <summary>
        /// XML 1.0 §4.6: без DTD объявленными считаются ровно пять сущностей.
        /// Ссылка на любую другую - фатальная ошибка (§4.1, WFC: Entity Declared),
        /// а не повод оставить текст как есть.
        /// </summary>
        private static void WritePredefinedEntity(
            roschar name,
            int start,
            Span<char> destination,
            ref int written
            )
        {
            char replacement;

            switch (name.Length)
            {
                case 2 when name[0] == 'l' && name[1] == 't':
                    replacement = '<';
                    break;
                case 2 when name[0] == 'g' && name[1] == 't':
                    replacement = '>';
                    break;
                case 3 when name[0] == 'a' && name[1] == 'm' && name[2] == 'p':
                    replacement = '&';
                    break;
                case 4 when name[0] == 'a' && name[1] == 'p' && name[2] == 'o' && name[3] == 's':
                    replacement = '\'';
                    break;
                case 4 when name[0] == 'q' && name[1] == 'u' && name[2] == 'o' && name[3] == 't':
                    replacement = '"';
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Reference to undeclared entity '&{name.ToString()};' at position {start}. Without a DTD, XML 1.0 §4.6 declares only amp, lt, gt, apos and quot."
                        );
            }

            destination[written++] = replacement;
        }

        /// <summary>
        /// XML 1.0 §4.1: CharRef ::= '&amp;#' [0-9]+ ';' | '&amp;#x' [0-9a-fA-F]+ ';'
        /// Признак шестнадцатеричной формы - строчная 'x'; 'X' спецификацией
        /// не предусмотрена.
        /// </summary>
        private static void WriteCharacterReference(
            roschar digits,
            int start,
            Span<char> destination,
            ref int written
            )
        {
            var hexadecimal = !digits.IsEmpty && digits[0] == 'x';
            if (hexadecimal)
            {
                digits = digits.Slice(1);
            }

            if (digits.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Character reference at position {start} has no digits (XML 1.0 §4.1)."
                    );
            }

            var codePoint = 0;
            for (var i = 0; i < digits.Length; i++)
            {
                var digit = DigitValue(digits[i], hexadecimal);
                if (digit < 0)
                {
                    throw new InvalidOperationException(
                        $"Character reference at position {start} contains '{digits[i]}', which is not a {(hexadecimal ? "hexadecimal" : "decimal")} digit (XML 1.0 §4.1)."
                        );
                }

                codePoint = (codePoint * (hexadecimal ? 16 : 10)) + digit;

                //дальше считать незачем: значение уже вне диапазона Unicode,
                //а без этой проверки длинная ссылка переполнила бы int
                if (codePoint > 0x10FFFF)
                {
                    ThrowIllegalCodePoint(codePoint, start);
                }
            }

            if (!IsLegalXmlChar(codePoint))
            {
                ThrowIllegalCodePoint(codePoint, start);
            }

            if (codePoint <= 0xFFFF)
            {
                destination[written++] = (char)codePoint;
                return;
            }

            //за пределами BMP - суррогатная пара
            var shifted = codePoint - 0x10000;
            destination[written++] = (char)(0xD800 + (shifted >> 10));
            destination[written++] = (char)(0xDC00 + (shifted & 0x3FF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DigitValue(char c, bool hexadecimal)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (!hexadecimal)
            {
                return -1;
            }

            if (c >= 'a' && c <= 'f')
            {
                return (c - 'a') + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return (c - 'A') + 10;
            }

            return -1;
        }

        /// <summary>
        /// XML 1.0 §2.2:
        /// Char ::= #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
        /// Ссылка обязана разрешаться в легальный символ (§4.1, WFC: Legal Character),
        /// поэтому суррогаты и #x0 отвергаются здесь, а не проскакивают в строку.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLegalXmlChar(int codePoint)
        {
            if (codePoint == 0x9 || codePoint == 0xA || codePoint == 0xD)
            {
                return true;
            }

            if (codePoint >= 0x20 && codePoint <= 0xD7FF)
            {
                return true;
            }

            if (codePoint >= 0xE000 && codePoint <= 0xFFFD)
            {
                return true;
            }

            return codePoint >= 0x10000 && codePoint <= 0x10FFFF;
        }

        private static void ThrowIllegalCodePoint(int codePoint, int start)
        {
            throw new InvalidOperationException(
                $"Character reference at position {start} resolves to U+{codePoint:X4}, which is not a legal XML 1.0 character (§2.2)."
                );
        }

        #endregion
    }
}
