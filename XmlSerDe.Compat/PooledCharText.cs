using System;
using System.Buffers;
using System.IO;

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
