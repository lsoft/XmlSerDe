using System;
using System.Runtime.CompilerServices;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// XML length estimator: integers via a log2 digit table,
    /// DateTime / TimeSpan / decimal via exact lexical width
    /// (<see cref="XmlLexicalLength"/>). Used to size
    /// <see cref="PooledCharExhauster"/> before the write pass.
    /// Not thread-safe.
    /// </summary>
    public sealed class LengthEstimatorExhauster : IExhauster
    {
        private int _totalLength;

        public int EstimatedTotalLength => _totalLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountUInt32(uint value)
            => DecimalDigitsLog.AtLeast(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountUInt64(ulong value)
            => DecimalDigitsLog.AtLeast(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountSigned32(int value)
        {
            var u = (uint)value;
            if (value < 0)
            {
                return 1 + CountUInt32(0u - u);
            }

            return CountUInt32(u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountSigned64(long value)
        {
            var u = (ulong)value;
            if (value < 0)
            {
                return 1 + CountUInt64(0ul - u);
            }

            return CountUInt64(u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _totalLength = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime value)
        {
            _totalLength += XmlLexicalLength.DateTime(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
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
            _totalLength += CountSigned32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountSigned32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value)
        {
            _totalLength += CountUInt32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountUInt32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort value)
        {
            _totalLength += CountUInt32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountUInt32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value)
        {
            _totalLength += CountSigned32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountSigned32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value)
        {
            _totalLength += CountUInt32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountUInt32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value)
        {
            _totalLength += CountSigned32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountSigned32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value)
        {
            _totalLength += CountUInt64(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountUInt64(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value)
        {
            _totalLength += CountSigned64(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountSigned64(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value)
        {
            _totalLength += XmlLexicalLength.Decimal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Append(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(float value)
        {
            _totalLength += 20;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(float? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += 20;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double value)
        {
            _totalLength += 30;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += 30;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char value)
        {
            _totalLength += CountUInt32(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += CountUInt32(value.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan value)
        {
            _totalLength += XmlLexicalLength.TimeSpan(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan? value)
        {
            if (!value.HasValue)
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

            _totalLength += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            _totalLength += value.Length + CalculateOverheadFromXmlSpecialSymbol(value);
        }

        //оценщик длины строку не кодирует, поэтому guard'а здесь нет ни в одном
        //режиме и checked/unchecked считают одно и то же
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendEncodedUnchecked(string? value)
        {
            AppendEncoded(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendAttributeEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            _totalLength += value.Length + XmlAttributeEncoder.EstimateOverhead(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendAttributeEncodedUnchecked(string? value)
        {
            AppendAttributeEncoded(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendBase64(byte[]? value)
        {
            _totalLength += XmlBase64.EncodedLength(value);
        }

        private static int CalculateOverheadFromXmlSpecialSymbol(string value)
        {
            // HtmlEncode пишет апостроф как &#39; (+4); без него оценка
            // проседает ровно на эти 4 символа на REGULAR (RawString).
            const string XmlSpecialCharacters = "<>&`'\"";

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

                specialSymbolOverhead += (5 - 1);
                span = span.Slice(index + 1);
            }

            return specialSymbolOverhead;
        }
    }
}
