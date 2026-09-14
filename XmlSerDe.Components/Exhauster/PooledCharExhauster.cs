using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using XmlSerDe;
using XmlSerDe.Internal;

namespace XmlSerDe
{
    /// <summary>
    /// Строковой сток: документ лежит в одном <c>char[]</c> из пула.
    /// После прохода оценщика длины этот массив - верхняя оценка;
    /// <see cref="ToString"/> копирует только записанный префикс в результат,
    /// <see cref="Dispose"/> возвращает массив в пул.
    /// Не потокобезопасен.
    ///
    /// Свой пул, а не <see cref="ArrayPool{T}.Shared"/>: Shared на netstandard2.0 /
    /// net472 не удерживает массивы крупнее ~1 МБ элементов, а на net8+ - крупнее
    /// 2^27. HUGE-документ (~100 МБ символов) туда часто не помещается,
    /// и Rent каждый раз выделял бы новый буфер - Allocated снова
    /// был бы «буфер + строка».
    /// </summary>
    public sealed class PooledCharExhauster : ExhausterBase, IDisposable
    {
        private static readonly ArrayPool<char> Pool = ArrayPool<char>.Create(
            maxArrayLength: 512 * 1024 * 1024,
            maxArraysPerBucket: 2
            );

        private readonly string _dateTimeFormat;
        private char[] _buffer;
        private int _pos;
        private bool _disposed;

        public PooledCharExhauster(
            int capacity,
            string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
            )
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (dateTimeFormat is null)
            {
                throw new ArgumentNullException(nameof(dateTimeFormat));
            }

            _dateTimeFormat = dateTimeFormat;
            _buffer = Pool.Rent(Math.Max(capacity, 16));
            _pos = 0;
        }

        public int Written
        {
            get
            {
                ThrowIfDisposed();
                return _pos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ThrowIfDisposed();
            _pos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            ThrowIfDisposed();
            if (_pos == 0)
            {
                return string.Empty;
            }

            return new string(_buffer, 0, _pos);
        }

        /// <summary>
        /// Пишет записанный префикс в сток без промежуточной <c>string</c>.
        /// </summary>
        public void WriteTo(TextWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            ThrowIfDisposed();
            if (_pos == 0)
            {
                return;
            }

#if NET8_0_OR_GREATER
            writer.Write(_buffer.AsSpan(0, _pos));
#else
            writer.Write(_buffer, 0, _pos);
#endif
        }

        /// <summary>
        /// То же для <see cref="XmlWriter.WriteRaw(char[], int, int)"/>.
        /// </summary>
        public void WriteRawTo(XmlWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            ThrowIfDisposed();
            if (_pos == 0)
            {
                return;
            }

            writer.WriteRaw(_buffer, 0, _pos);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var buffer = _buffer;
            _buffer = Array.Empty<char>();
            _pos = 0;
            Pool.Return(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledCharExhauster));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Ensure(int additional)
        {
            ThrowIfDisposed();
            var needed = _pos + additional;
            if (needed < _pos)
            {
                throw new OverflowException("XML length exceeds Int32.MaxValue characters.");
            }

            if (needed <= _buffer.Length)
            {
                return;
            }

            var newSize = _buffer.Length;
            if (newSize > int.MaxValue / 2)
            {
                newSize = int.MaxValue;
            }
            else
            {
                newSize *= 2;
            }

            if (newSize < needed)
            {
                newSize = needed;
            }

            var next = Pool.Rent(newSize);
            Array.Copy(_buffer, 0, next, 0, _pos);
            Pool.Return(_buffer);
            _buffer = next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendSpan(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
            {
                return;
            }

            Ensure(chars.Length);
            chars.CopyTo(_buffer.AsSpan(_pos));
            _pos += chars.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendLiteral(string? value)
        {
            if (value is null || value.Length == 0)
            {
                return;
            }

            Ensure(value.Length);
            value.AsSpan().CopyTo(_buffer.AsSpan(_pos));
            _pos += value.Length;
        }

#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendInvariant<T>(T value) where T : ISpanFormattable
        {
            Span<char> buffer = stackalloc char[40];
            if (value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
            {
                AppendSpan(buffer.Slice(0, written));
            }
            else
            {
                AppendLiteral(value.ToString(null, CultureInfo.InvariantCulture));
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendInvariant<T>(T value) where T : IFormattable
        {
            AppendLiteral(value.ToString(null, CultureInfo.InvariantCulture));
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(DateTime value)
        {
#if NET8_0_OR_GREATER
            Span<char> buffer = stackalloc char[64];
            if (value.TryFormat(buffer, out var written, _dateTimeFormat.AsSpan(), CultureInfo.InvariantCulture))
            {
                AppendSpan(buffer.Slice(0, written));
            }
            else
            {
                AppendLiteral(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture));
            }
#else
            AppendLiteral(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(Guid value)
        {
#if NET8_0_OR_GREATER
            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out var written))
            {
                AppendSpan(buffer.Slice(0, written));
            }
            else
            {
                AppendLiteral(value.ToString());
            }
#else
            AppendLiteral(value.ToString());
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(Guid? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(bool value)
        {
            AppendLiteral(value ? "true" : "false");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendLiteral(value.Value ? "true" : "false");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(sbyte value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(sbyte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(byte value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ushort value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(short value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(short? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(uint value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ulong value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ulong? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(long value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(long? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(decimal value) => AppendInvariant(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(decimal? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendInvariant(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(double value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                AppendLiteral(special);
                return;
            }

            AppendRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(double? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(float value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                AppendLiteral(special);
                return;
            }

            AppendRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(float? value)
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
                AppendSpan(buffer.Slice(0, written));
            }
            else
            {
                AppendLiteral(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendRoundtrip<T>(T value) where T : IFormattable
        {
            AppendLiteral(value.ToString("R", CultureInfo.InvariantCulture));
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(char value)
        {
            AppendInvariant((ushort)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(char? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(TimeSpan value)
        {
            Span<char> buffer = stackalloc char[32];
            if (!XmlNumberLexis.TryFormat(buffer, value, out var written))
            {
                AppendLiteral(XmlNumberLexis.ToDurationString(value));
                return;
            }

            AppendSpan(buffer.Slice(0, written));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(TimeSpan? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            AppendLiteral(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            XmlCharGuard.EnsureValidXmlChars(value.AsSpan());
            var dest = new EncodeSink(this);
            XmlTextEncoder.EncodeTo(value, ref dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncodedUnchecked(string? value)
        {
            if (value is null)
            {
                return;
            }

            var dest = new EncodeSink(this);
            XmlTextEncoder.EncodeTo(value, ref dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendAttributeEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            var dest = new EncodeSink(this);
            XmlAttributeEncoder.WriteToChecked(value, ref dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendAttributeEncodedUnchecked(string? value)
        {
            if (value is null)
            {
                return;
            }

            var dest = new EncodeSink(this);
            XmlAttributeEncoder.WriteTo(value, ref dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendBase64(byte[]? value)
        {
            if (value is null)
            {
                return;
            }

            var charCount = XmlBase64.EncodedLength(value);
            if (charCount == 0)
            {
                return;
            }

            Ensure(charCount);
            _pos += XmlBase64.EncodeToChars(value, _buffer, _pos);
        }

        private readonly struct EncodeSink : IXmlEncodeDestination
        {
            private readonly PooledCharExhauster _exhauster;

            public EncodeSink(PooledCharExhauster exhauster)
            {
                _exhauster = exhauster;
            }

            public void WriteSlice(string value, int start, int count)
            {
                if (count == 0)
                {
                    return;
                }

                _exhauster.AppendSpan(value.AsSpan(start, count));
            }

            public void WriteLiteral(string literal)
            {
                _exhauster.AppendLiteral(literal);
            }

            public void WriteChars(ReadOnlySpan<char> chars)
            {
                _exhauster.AppendSpan(chars);
            }
        }
    }
}
