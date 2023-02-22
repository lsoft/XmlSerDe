using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace XmlSerDe.Common
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
            var svalue = value.ToString(_dateTimeFormat); //todo: want to "stringificate" to existing Span<char>

            var valueLength = svalue.Length;
            if (valueLength <= CharCountBufferSize)
            {
                //use internal buffer
                var byteCount = Encoding.UTF8.GetBytes(svalue, 0, valueLength, _internalBuffer, 0);
                Write(_internalBuffer, byteCount);
            }
            else
            {
                //rent the buffer
                var rented = ArrayPool<byte>.Shared.Rent(valueLength * 2);
                var byteCount = Encoding.UTF8.GetBytes(svalue, 0, valueLength, rented, 0);
                Write(rented, byteCount);
                ArrayPool<byte>.Shared.Return(rented); //nothing catastrophic happens if there will be an exception before this line; please read the doc of renting
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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
            var svalue = value.ToString(); //todo: want to "stringificate" to existing Span<char>
            var byteCount = Encoding.UTF8.GetBytes(svalue, 0, svalue.Length, _internalBuffer, 0);
            Write(_internalBuffer, byteCount);
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

    /// <summary>
    /// Estimator of total XML string length.
    /// It is designed to return excessive estimation,
    /// because it's better to allocate 10% more, that reallocate
    /// if few bytes had not been enough in the buffer.
    /// Class is NOT a thread-safe!
    /// </summary>
    public class DefaultLengthEstimatorExhauster : IExhauster
    {
        private int _totalLength;
        private readonly int _dateTimeLength;

        /// <summary>
        /// Estimated total char count (not bytes!).
        /// </summary>
        public int EstimatedTotalLength => _totalLength;

        public DefaultLengthEstimatorExhauster(
            int dateTimeLength = 33
            )
        {

            _dateTimeLength = dateTimeLength;
            _totalLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _totalLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
            _totalLength += _dateTimeLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += _dateTimeLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid value)
        {
            _totalLength += 36;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += 36;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool value)
        {
            _totalLength += (value ? 4 : 5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += (value.Value ? 4 : 5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte value)
        {
            //estimate at top limit
            _totalLength += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value)
        {
            //estimate at top limit
            _totalLength += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort value)
        {
            //estimate at top limit
            _totalLength += 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value)
        {
            //estimate at top limit
            _totalLength += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value)
        {
            //estimate at top limit
            _totalLength += 10;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 10;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value)
        {
            //estimate at top limit
            _totalLength += 11;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 11;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value)
        {
            //estimate at top limit
            _totalLength += 20;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 20;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value)
        {
            //estimate at top limit
            _totalLength += 21;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 21;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value)
        {
            //estimate at top limit
            _totalLength += 30;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            //estimate at top limit
            _totalLength += 30;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? value)
        {
            if (value is null)
            {
                return;
            }

            _totalLength += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            var specialSymbolOverhead = CalculateOverheadFromXmlSpecialSymbol(
                value
                );

            _totalLength += value!.Length + specialSymbolOverhead;
        }

        private static int CalculateOverheadFromXmlSpecialSymbol(string value)
        {
            const string XmlSpecialCharacters = "<>&`\"";

            var span = value.AsSpan();
            var sspan = XmlSpecialCharacters.AsSpan();

            var specialSymbolOverhead = 0;
            while (!span.IsEmpty)
            {
                var index = MemoryExtensions.IndexOfAny(span, sspan);
                if (index < 0)
                {
                    break;
                }

                //estimate at top limit (must be 5, but -1 symbol here due to we have an one symbol per every special symbol in the incoming string)
                specialSymbolOverhead += (5 - 1);

                span = span.Slice(index + 1);
            }

            return specialSymbolOverhead;
        }
    }

    /// <summary>
    /// Exhauster that writes into StringBuilder.
    /// Class is NOT a thread-safe!
    /// </summary>
    public class DefaultStringBuilderExhauster : IExhauster
    {
        private readonly StringBuilder _sb;
        private readonly string _dateTimeFormat;

        public DefaultStringBuilderExhauster(
            StringBuilder? sb = null,
            string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffK"
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
            _sb.Append(value.ToString(_dateTimeFormat));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _sb.Append(value.Value.ToString(_dateTimeFormat));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(Guid? value)
        {
            _sb.Append(value);
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
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? value)
        {
            _sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncoded(string? value)
        {
            var encoded = global::System.Net.WebUtility.HtmlEncode(value);
            _sb.Append(encoded);
        }
    }

    public interface IExhauster
    {
        void Append(DateTime value);
        void Append(DateTime? value);

        void Append(Guid value);
        void Append(Guid? value);

        void Append(bool value);
        void Append(bool? value);

        void Append(sbyte value);
        void Append(sbyte? value);

        void Append(byte value);
        void Append(byte? value);

        void Append(ushort value);
        void Append(ushort? value);

        void Append(short value);
        void Append(short? value);

        void Append(uint value);
        void Append(uint? value);

        void Append(int value);
        void Append(int? value);

        void Append(ulong value);
        void Append(ulong? value);

        void Append(long value);
        void Append(long? value);

        void Append(decimal value);
        void Append(decimal? value);

        void Append(string? value);
        void AppendEncoded(string? value);
    }
}
