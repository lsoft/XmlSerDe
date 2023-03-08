using System;
using System.Runtime.CompilerServices;
using System.Text;
using XmlSerDe.Common;

namespace XmlSerDe.Components.Exhauster
{
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
}
