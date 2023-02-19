using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace XmlSerDe.Generator.EmbeddedCode
{
    /// <summary>
    /// Estimator of total XML string length.
    /// It is designed to return excessive estimation,
    /// better to allocate 10% more, that reallocate
    /// if few bytes had not been enough in the buffer.
    /// </summary>
    public class DefaultLengthEstimatorExhauster : IExhauster
    {
        private int _totalLength;
        private readonly int _dateTimeLength;

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

            const string XmlSpecialCharacters = "<>&`\"";

            var span = value.AsSpan();
            var sspan = XmlSpecialCharacters.AsSpan();

            var specialSymbolCount = 0;
            while (!span.IsEmpty)
            {
                var index = MemoryExtensions.IndexOfAny(span, sspan);
                if (index < 0)
                {
                    break;
                }

                //estimate at top limit
                specialSymbolCount += 4;

                span = span.Slice(index + 1);
            }

            _totalLength += value!.Length + specialSymbolCount;
        }

    }

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
