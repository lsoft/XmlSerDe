using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Документ из <see cref="TextReader"/> в арендованном <c>char[]</c>, без
    /// <c>ReadToEnd</c>: тот копит в <see cref="System.Text.StringBuilder"/> и
    /// потом копирует в строку, которая парсеру не нужна.
    /// </summary>
    internal struct PooledCharText : IDisposable
    {
        private char[] _buffer;
        private int _length;

        public ReadOnlySpan<char> Span => _buffer.AsSpan(0, _length);

        public static PooledCharText ReadAll(TextReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var rented = ArrayPool<char>.Shared.Rent(4096);
            var count = 0;

            try
            {
                while (true)
                {
                    if (count == rented.Length)
                    {
                        var next = ArrayPool<char>.Shared.Rent(rented.Length * 2);
                        Array.Copy(rented, 0, next, 0, count);
                        ArrayPool<char>.Shared.Return(rented);
                        rented = next;
                    }

                    var read = reader.Read(rented, count, rented.Length - count);
                    if (read == 0)
                    {
                        break;
                    }

                    count += read;
                }

                return new PooledCharText
                {
                    _buffer = rented,
                    _length = count
                };
            }
            catch
            {
                ArrayPool<char>.Shared.Return(rented);
                throw;
            }
        }

        /// <summary>
        /// Документ из потока байтов. Кодировку выбирает сам документ, как у
        /// <see cref="System.Xml.XmlTextReader"/>: BOM, затем порядок первых байтов
        /// (UTF-16 без BOM), затем <c>encoding</c> в объявлении <c>&lt;?xml?&gt;</c>,
        /// и только потом UTF-8. UTF-8 по умолчанию строгая: невалидный байт - это
        /// ошибка документа, а не U+FFFD в значении.
        /// </summary>
        public static PooledCharText ReadAll(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var bytes = ArrayPool<byte>.Shared.Rent(16384);
            try
            {
                var count = 0;
                while (true)
                {
                    if (count == bytes.Length)
                    {
                        var next = ArrayPool<byte>.Shared.Rent(bytes.Length * 2);
                        Array.Copy(bytes, 0, next, 0, count);
                        ArrayPool<byte>.Shared.Return(bytes);
                        bytes = next;
                    }

                    var read = stream.Read(bytes, count, bytes.Length - count);
                    if (read == 0)
                    {
                        break;
                    }

                    count += read;
                }

                var encoding = DetectEncoding(bytes, count, out var preambleLength);
                var payload = count - preambleLength;

                var chars = ArrayPool<char>.Shared.Rent(Math.Max(1, encoding.GetMaxCharCount(payload)));
                int written;
                try
                {
                    written = encoding.GetChars(bytes, preambleLength, payload, chars, 0);
                }
                catch
                {
                    ArrayPool<char>.Shared.Return(chars);
                    throw;
                }

                return new PooledCharText
                {
                    _buffer = chars,
                    _length = written
                };
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private static Encoding DetectEncoding(byte[] bytes, int count, out int preambleLength)
        {
            preambleLength = 0;

            if (count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                preambleLength = 3;
                return StrictUtf8;
            }
            if (count >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                preambleLength = 4;
                return new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true);
            }
            if (count >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                preambleLength = 4;
                return new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true);
            }
            if (count >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                preambleLength = 2;
                return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
            }
            if (count >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                preambleLength = 2;
                return new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
            }

            //UTF-16 без BOM узнаётся по первому '<': XmlTextReader делает то же
            if (count >= 2 && bytes[0] == 0x3C && bytes[1] == 0x00)
            {
                return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
            }
            if (count >= 2 && bytes[0] == 0x00 && bytes[1] == 0x3C)
            {
                return new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
            }

            var declared = DeclaredEncodingName(bytes, count);
            if (declared is null)
            {
                return StrictUtf8;
            }

            if (string.Equals(declared, "utf-8", StringComparison.OrdinalIgnoreCase))
            {
                return StrictUtf8;
            }

            //неизвестное имя - исключение, как у XmlTextReader ("System does not
            //support 'X' encoding"); наружу оно уйдёт завёрнутым, как всякая ошибка чтения
            return Encoding.GetEncoding(declared);
        }

        /// <summary>
        /// Значение <c>encoding</c> из объявления <c>&lt;?xml ... ?&gt;</c>, если
        /// документ с него начинается. Объявление по определению в ASCII-диапазоне,
        /// поэтому его можно читать побайтно, ещё не зная кодировки.
        /// </summary>
        private static string? DeclaredEncodingName(byte[] bytes, int count)
        {
            const string head = "<?xml";
            if (count < head.Length)
            {
                return null;
            }
            for (var i = 0; i < head.Length; i++)
            {
                if (bytes[i] != head[i])
                {
                    return null;
                }
            }

            var end = head.Length;
            while (end + 1 < count && !(bytes[end] == '?' && bytes[end + 1] == '>'))
            {
                end++;
            }

            var declaration = Encoding.ASCII.GetString(bytes, 0, Math.Min(end + 2, count));
            var at = declaration.IndexOf("encoding", StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            var eq = declaration.IndexOf('=', at);
            if (eq < 0)
            {
                return null;
            }

            var open = eq + 1;
            while (open < declaration.Length && (declaration[open] == ' ' || declaration[open] == '\t' || declaration[open] == '\r' || declaration[open] == '\n'))
            {
                open++;
            }
            if (open >= declaration.Length || (declaration[open] != '"' && declaration[open] != '\''))
            {
                return null;
            }

            var close = declaration.IndexOf(declaration[open], open + 1);
            if (close < 0)
            {
                return null;
            }

            return declaration.Substring(open + 1, close - open - 1);
        }

        public void Dispose()
        {
            var buffer = _buffer;
            _buffer = null!;
            _length = 0;
            if (buffer is not null)
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }
}
