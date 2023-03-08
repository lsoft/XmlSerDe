using System;

namespace XmlSerDe.Common
{
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
