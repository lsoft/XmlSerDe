using System;
using System.Runtime.CompilerServices;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
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
        public void Append(float value)
        {
            //кратчайшее round-trippable представление float не длиннее 14 символов
            //("-3.4028235E+38"), запас взят с потолка вверх
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
            //"-1.7976931348623157E+308" - 24 символа, дальше запас
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
            //пишется кодовой точкой: не больше пяти цифр
            _totalLength += 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _totalLength += 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan value)
        {
            //"-P10675199DT2H48M5.4775808S" - 26 символов на TimeSpan.MinValue
            _totalLength += 30;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(TimeSpan? value)
        {
            if (!value.HasValue)
            {
                return;
            }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendAttributeEncoded(string? value)
        {
            if (value is null)
            {
                return;
            }

            _totalLength += value.Length + XmlAttributeEncoder.EstimateOverhead(value);
        }

        /// <summary>
        /// Единственное место, где оценщик не повторяет работу писателя, а считает:
        /// длина base64 известна по длине массива, и кодировать его ради одной
        /// только длины - выбросить строку сразу после того, как её построили.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendBase64(byte[]? value)
        {
            _totalLength += XmlBase64.EncodedLength(value);
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
}
