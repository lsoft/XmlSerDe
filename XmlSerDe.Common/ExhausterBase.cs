//Базовый класс экзостера. Реализовывать <see cref="IExhauster"/> напрямую
//генератор больше не даёт, и вот зачем.
//
//Интерфейс - контракт «всё или ничего»: любой новый член ломает по исходникам
//и по бинарю каждого, кто его реализовал, то есть стоит мажорной версии. За
//две недели это случилось дважды (AppendEncodedUnchecked и
//AppendAttributeEncodedUnchecked). Класс такого свойства не имеет: новый член
//добавляется virtual с реализацией по умолчанию, и наследники не замечают
//ничего.
//
//ПРАВИЛО, ради которого всё и затевалось: новый член добавляется в этот класс
//ТОЛЬКО как virtual с работающим телом по умолчанию. Тело выражается через
//Append(string?) - единственный примитив, который обязан быть у любого стока и
//который ничего не экранирует. Пример будущего добавления:
//
//    public virtual void Append(Half value)
//    {
//        Append(value.ToString("R", CultureInfo.InvariantCulture));
//    }
//
//Такой член стоит аллокации у тех, кто его не переопределил, - и ровно ноль у
//тех, кому он не нужен. Добавить его abstract'ом значит вернуть ту самую
//ломкость, от которой ушли.
//
//Сегодняшние 38 членов abstract намеренно: реализации у всех стоков уже есть,
//а придумывать им общий default значило бы дублировать форматирование, которое
//у каждого стока своё и вылизано под свой буфер.
//
//Второе правило - к производительности: генератор пишет вызов по конкретному
//типу, поэтому запечатанный наследник девиртуализуется и стоит ровно столько
//же, сколько невиртуальный метод (docs/dispatch-cost.md). Незапечатанный
//стоит дороже, и генератор об этом предупреждает.

using System;

namespace XmlSerDe
{
    public abstract class ExhausterBase : IExhauster
    {
        public abstract void Append(DateTime value);
        public abstract void Append(DateTime? value);
        public abstract void Append(Guid value);
        public abstract void Append(Guid? value);
        public abstract void Append(bool value);
        public abstract void Append(bool? value);
        public abstract void Append(sbyte value);
        public abstract void Append(sbyte? value);
        public abstract void Append(byte value);
        public abstract void Append(byte? value);
        public abstract void Append(ushort value);
        public abstract void Append(ushort? value);
        public abstract void Append(short value);
        public abstract void Append(short? value);
        public abstract void Append(uint value);
        public abstract void Append(uint? value);
        public abstract void Append(int value);
        public abstract void Append(int? value);
        public abstract void Append(ulong value);
        public abstract void Append(ulong? value);
        public abstract void Append(long value);
        public abstract void Append(long? value);
        public abstract void Append(decimal value);
        public abstract void Append(decimal? value);
        public abstract void Append(float value);
        public abstract void Append(float? value);
        public abstract void Append(double value);
        public abstract void Append(double? value);
        public abstract void Append(char value);
        public abstract void Append(char? value);
        public abstract void Append(TimeSpan value);
        public abstract void Append(TimeSpan? value);
        public abstract void Append(string? value);
        public abstract void AppendEncoded(string? value);
        public abstract void AppendEncodedUnchecked(string? value);
        public abstract void AppendAttributeEncoded(string? value);
        public abstract void AppendAttributeEncodedUnchecked(string? value);
        public abstract void AppendBase64(byte[]? value);
    }
}
