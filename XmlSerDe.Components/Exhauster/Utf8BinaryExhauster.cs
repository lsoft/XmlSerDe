using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// UTF-8 string to binary exhauster (mostly) based on rented buffers.
    /// Class is NOT a thread-safe!
    /// </summary>
    public abstract class Utf8BinaryExhauster : ExhausterBase
    {
        private static readonly byte[] _trueData = Encoding.UTF8.GetBytes("true");
        private static readonly byte[] _falseData = Encoding.UTF8.GetBytes("false");

        private readonly string _dateTimeFormat;

        /// <summary>
        /// Голова с <c>xsi:type</c> длиннее Guid (36). 256 символов покрывает
        /// типичный тег, и UTF-8 тогда всегда помещается в <see cref="_internalBuffer"/>.
        /// </summary>
        private const int CharCountBufferSize = 256;
        private readonly byte[] _internalBuffer;

        /// <param name="dateTimeFormat">
        /// См. <see cref="StringBuilderExhauster"/>: <c>FFFFFFF</c> даёт ту же
        /// лексическую форму, что и System.Xml.Serialization.
        /// </param>
        public Utf8BinaryExhauster(
            string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
            )
        {
            if (dateTimeFormat is null)
            {
                throw new ArgumentNullException(nameof(dateTimeFormat));
            }

            _dateTimeFormat = dateTimeFormat;
            _internalBuffer = new byte[Encoding.UTF8.GetMaxByteCount(CharCountBufferSize)];
        }

        /// <summary>
        /// Process binary data.
        /// THIS BUFFER MAY BE RENTED FROM A POOL!
        /// DO NOT USE THIS BUFFER AFTER THIS METHOD IS FINISHED ITS WORK!
        /// COPY THE DATA IF YOU NEED THIS DATA LATER!
        /// </summary>
        protected abstract void Write(byte[] data, int length);

        /// <summary>
        /// Writes UTF-8 bytes for the given chars, using the internal buffer
        /// when it fits, otherwise falling back to a rented buffer (mirrors
        /// the fallback strategy of <see cref="Append(string?)"/>).
        /// </summary>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteChars(ReadOnlySpan<char> chars)
        {
            //буфер рассчитан на GetMaxByteCount(CharCountBufferSize), поэтому короткий
            //срез кодируется одним проходом; GetByteCount перед GetBytes - это второй
            //проход по тем же символам, и он нужен только когда срез длиннее буфера
            if (chars.Length <= CharCountBufferSize)
            {
                var direct = Encoding.UTF8.GetBytes(chars, _internalBuffer);
                Write(_internalBuffer, direct);
                return;
            }

            var byteCount = Encoding.UTF8.GetByteCount(chars);
            if (byteCount <= _internalBuffer.Length)
            {
                var written = Encoding.UTF8.GetBytes(chars, _internalBuffer);
                Write(_internalBuffer, written);
            }
            else
            {
                var rented = ArrayPool<byte>.Shared.Rent(byteCount);
                var written = Encoding.UTF8.GetBytes(chars, rented);
                Write(rented, written);
                ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
            }
        }
#else
        /// <remarks>
        /// netstandard2.0 не имеет span-перегрузок Encoding, поэтому вход
        /// перекладывается в буфер из пула: аллокаций всё равно нет, но проход
        /// по символам получается лишний.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteChars(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
            {
                Write(_internalBuffer, 0);
                return;
            }

            var charBuffer = ArrayPool<char>.Shared.Rent(chars.Length);
            chars.CopyTo(charBuffer);

            var byteCount = Encoding.UTF8.GetByteCount(charBuffer, 0, chars.Length);
            if (byteCount <= _internalBuffer.Length)
            {
                var written = Encoding.UTF8.GetBytes(charBuffer, 0, chars.Length, _internalBuffer, 0);
                Write(_internalBuffer, written);
            }
            else
            {
                var rented = ArrayPool<byte>.Shared.Rent(byteCount);
                var written = Encoding.UTF8.GetBytes(charBuffer, 0, chars.Length, rented, 0);
                Write(rented, written);
                ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
            }

            ArrayPool<char>.Shared.Return(charBuffer);
        }
#endif

        /// <summary>
        /// Formats a value using an XSD-compatible, culture-invariant lexical
        /// representation (decimal point, ASCII digits) regardless of the
        /// current thread's culture, then writes it as UTF-8 bytes.
        /// </summary>
#if NET8_0_OR_GREATER
        /// <remarks>
        /// Сразу в байты: числа, даты и Guid умеют IUtf8SpanFormattable, и
        /// промежуточные UTF-16 символы с их перекодированием здесь лишние.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInvariant<T>(T value) where T : IUtf8SpanFormattable, IFormattable
        {
            if (value.TryFormat(_internalBuffer, out var bytesWritten, default, CultureInfo.InvariantCulture))
            {
                Write(_internalBuffer, bytesWritten);
            }
            else
            {
                WriteChars(value.ToString(null, CultureInfo.InvariantCulture).AsSpan());
            }
        }
#else
        /// <remarks>
        /// netstandard2.0 не знает ISpanFormattable, поэтому здесь остаётся
        /// форматирование через промежуточную строку.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInvariant<T>(T value) where T : IFormattable
        {
            WriteChars(value.ToString(null, CultureInfo.InvariantCulture).AsSpan());
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(DateTime value)
        {
#if NET8_0_OR_GREATER
            if (value.TryFormat(_internalBuffer, out var bytesWritten, _dateTimeFormat, CultureInfo.InvariantCulture))
            {
                Write(_internalBuffer, bytesWritten);
            }
            else
            {
                WriteChars(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture).AsSpan());
            }
#else
            WriteChars(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture).AsSpan());
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(DateTime? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(Guid value)
        {
#if NET8_0_OR_GREATER
            if (value.TryFormat(_internalBuffer, out var bytesWritten))
            {
                Write(_internalBuffer, bytesWritten);
            }
            else
            {
                WriteChars(value.ToString().AsSpan());
            }
#else
            WriteChars(value.ToString().AsSpan());
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(Guid? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(bool value)
        {
            if (value)
            {
                Write(_trueData, _trueData.Length);
            }
            else
            {
                Write(_falseData, _falseData.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(bool? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(sbyte value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(sbyte? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(byte value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(byte? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ushort value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ushort? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(short value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(short? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(uint value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(uint? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ulong value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(ulong? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(long value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(long? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(decimal value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(decimal? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        /// <summary>
        /// Формат "R", а не по умолчанию: см. <see cref="StringBuilderExhauster"/>.
        /// </summary>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteRoundtrip<T>(T value) where T : IUtf8SpanFormattable, IFormattable
        {
            if (value.TryFormat(_internalBuffer, out var bytesWritten, "R", CultureInfo.InvariantCulture))
            {
                Write(_internalBuffer, bytesWritten);
            }
            else
            {
                WriteChars(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteRoundtrip<T>(T value) where T : IFormattable
        {
            WriteChars(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(float value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                WriteChars(special.AsSpan());
                return;
            }

            WriteRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(float? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(double value)
        {
            var special = XmlNumberLexis.SpecialOrNull(value);
            if (special is not null)
            {
                WriteChars(special.AsSpan());
                return;
            }

            WriteRoundtrip(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(double? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(char value)
        {
            //кодовая точка, а не символ - см. IExhauster
            WriteInvariant((ushort)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(char? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(TimeSpan value)
        {
            Span<char> buffer = stackalloc char[XmlNumberLexis.MaxDurationLength];
            XmlNumberLexis.TryFormat(buffer, value, out var written);
#if NET8_0_OR_GREATER
            //xsd:duration - чистый ASCII, сужение вместо перекодирования
            System.Text.Ascii.FromUtf16(buffer.Slice(0, written), _internalBuffer, out var bytesWritten);
            Write(_internalBuffer, bytesWritten);
#else
            WriteChars(buffer.Slice(0, written));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(TimeSpan? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            if (value is null)
            {
                return;
            }

            var valueLength = value.Length;
            if (valueLength <= CharCountBufferSize)
            {
                var byteCount = Encoding.UTF8.GetBytes(value, 0, valueLength, _internalBuffer, 0);
                Write(_internalBuffer, byteCount);
            }
            else
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                var rented = ArrayPool<byte>.Shared.Rent(byteCount);
                var written = Encoding.UTF8.GetBytes(value, 0, valueLength, rented, 0);
                Write(rented, written);
                ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
            }
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

            var byteCount = XmlBase64.EncodedLength(value);
            if (byteCount == 0)
            {
                return;
            }

            if (byteCount <= _internalBuffer.Length)
            {
                var written = XmlBase64.EncodeToUtf8(value, _internalBuffer);
                Write(_internalBuffer, written);
                return;
            }

            var rented = ArrayPool<byte>.Shared.Rent(byteCount);
            var writtenRented = XmlBase64.EncodeToUtf8(value, rented);
            Write(rented, writtenRented);
            ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
        }

        private readonly struct EncodeSink : IXmlEncodeDestination
        {
            private readonly Utf8BinaryExhauster _exhauster;

            public EncodeSink(Utf8BinaryExhauster exhauster)
            {
                _exhauster = exhauster;
            }

            public void WriteSlice(string value, int start, int count)
            {
                if (count == 0)
                {
                    return;
                }

                _exhauster.WriteChars(value.AsSpan(start, count));
            }

            public void WriteLiteral(string literal)
            {
                _exhauster.Append(literal);
            }

            public void WriteChars(ReadOnlySpan<char> chars)
            {
                _exhauster.WriteChars(chars);
            }
        }
    }
}
