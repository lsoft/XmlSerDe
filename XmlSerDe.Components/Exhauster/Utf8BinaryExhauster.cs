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
    public abstract class Utf8BinaryExhauster : IExhauster
    {
        private static readonly byte[] _trueData = Encoding.UTF8.GetBytes("true");
        private static readonly byte[] _falseData = Encoding.UTF8.GetBytes("false");

        private readonly string _dateTimeFormat;

        private const int CharCountBufferSize = 36; //36 = char count in Guid.ToString()
        private readonly byte[] _internalBuffer = new byte[CharCountBufferSize * 2];

        public Utf8BinaryExhauster(
            string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffK"
            )
        {
            if (dateTimeFormat is null)
            {
                throw new ArgumentNullException(nameof(dateTimeFormat));
            }

            _dateTimeFormat = dateTimeFormat;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteChars(ReadOnlySpan<char> chars)
        {
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

        /// <summary>
        /// Formats a value using an XSD-compatible, culture-invariant lexical
        /// representation (decimal point, ASCII digits) regardless of the
        /// current thread's culture, then writes it as UTF-8 bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInvariant<T>(T value) where T : ISpanFormattable
        {
            Span<char> charBuffer = stackalloc char[40];
            if (value.TryFormat(charBuffer, out var charsWritten, default, CultureInfo.InvariantCulture))
            {
                WriteChars(charBuffer.Slice(0, charsWritten));
            }
            else
            {
                WriteChars(value.ToString(null, CultureInfo.InvariantCulture).AsSpan());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
            Span<char> charBuffer = stackalloc char[64];
            if (value.TryFormat(charBuffer, out var charsWritten, _dateTimeFormat.AsSpan(), CultureInfo.InvariantCulture))
            {
                WriteChars(charBuffer.Slice(0, charsWritten));
            }
            else
            {
                WriteChars(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture).AsSpan());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid value)
        {
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool value)
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
        public void Append(bool? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value)
        {
            WriteInvariant(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal? value)
        {
            if (value is null)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? value)
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
                var rented = ArrayPool<byte>.Shared.Rent(valueLength * 2);
                var byteCount = Encoding.UTF8.GetBytes(value, 0, valueLength, rented, 0);
                Write(rented, byteCount);
                ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            var encoded = global::System.Net.WebUtility.HtmlEncode(value);
            Append(encoded);
        }
    }
}
