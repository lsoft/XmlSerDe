using System;

namespace XmlSerDe
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
        /// Текст тела элемента: экранируется разметка, плюс проверка Char
        /// production XML 1.0 §2.2 (<see cref="XmlCharGuard"/>).
        /// </summary>
        void AppendEncoded(string? value);

        /// <summary>
        /// То же без <see cref="XmlCharGuard"/>: default-путь генератора,
        /// у которого <see cref="XmlFeature.CharGuard"/> выключен.
        ///
        /// Это отдельный метод, а не <c>Append(XmlTextEncoder.Encode(value))</c>
        /// на стороне генератора: <see cref="XmlTextEncoder.Encode"/> при наличии
        /// экранируемого символа строит промежуточную строку, тогда как
        /// экранирование прямо в буфер обходится без аллокации вовсе. Ради
        /// выключенной фичи платить аллокацией на каждую строку - ровно то, чего
        /// opt-in должен был избежать.
        /// </summary>
        void AppendEncodedUnchecked(string? value);

        /// <summary>
        /// Значение атрибута: сверх разметки экранируются ещё CR, LF и TAB, иначе
        /// читатель заменит их пробелом и строка с переводом строки не переживёт
        /// round-trip. См. <see cref="XmlAttributeEncoder"/>.
        /// </summary>
        void AppendAttributeEncoded(string? value);

        /// <summary>
        /// То же без <see cref="XmlCharGuard"/>.
        /// </summary>
        void AppendAttributeEncodedUnchecked(string? value);

        /// <summary>
        /// <c>byte[]</c> одной лексемой base64Binary. Экранирования здесь нет вовсе:
        /// в алфавите base64 нет ни разметки, ни пробельных символов, поэтому одна
        /// и та же лексема годится и в тело элемента, и в значение атрибута.
        /// См. <see cref="XmlBase64"/>.
        /// </summary>
        void AppendBase64(byte[]? value);
    }
}
