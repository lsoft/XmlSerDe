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

        void Append(float value);
        void Append(float? value);

        void Append(double value);
        void Append(double? value);

        /// <summary>
        /// Пишется кодовой точкой, а не символом: <c>'A'</c> превращается в
        /// <c>65</c>. Так делает System.Xml.Serialization, и это не произвол -
        /// в xsd типа для одиночного символа нет вовсе.
        /// </summary>
        void Append(char value);
        void Append(char? value);

        void Append(TimeSpan value);
        void Append(TimeSpan? value);

        void Append(string? value);

        /// <summary>
        /// Текст тела элемента: экранируется разметка.
        /// </summary>
        void AppendEncoded(string? value);

        /// <summary>
        /// Значение атрибута: сверх разметки экранируются ещё CR, LF и TAB, иначе
        /// читатель заменит их пробелом и строка с переводом строки не переживёт
        /// round-trip. См. <see cref="XmlAttributeEncoder"/>.
        /// </summary>
        void AppendAttributeEncoded(string? value);
    }
}
