//Базовый класс инжектора. Реализовывать <see cref="IInjector"/> напрямую
//генератор больше не даёт, и вот зачем.
//
//Интерфейс - контракт «всё или ничего»: любой новый член ломает по исходникам
//и по бинарю каждого, кто его реализовал, то есть стоит мажорной версии.
//Здесь это 66 членов, и снятие с Parse одного лишнего параметра сломало
//33 перегрузки разом. Класс такого свойства не имеет: новый член
//добавляется virtual с реализацией по умолчанию, и наследники не замечают
//ничего.
//
//ПРАВИЛО, ради которого всё и затевалось: новый член добавляется в этот класс
//ТОЛЬКО как virtual с работающим телом по умолчанию. Тело выражается через
//ParseBody - разбор из спана, не зависящий ни от какого состояния. Пример
//будущего добавления:
//
//    public virtual void ParseBody(roschar body, out Half value)
//    {
//        value = Half.Parse(body, CultureInfo.InvariantCulture);
//    }
//
//Такой член стоит лишней работы тем, кто его не переопределил, - и ровно
//ноль тем, кому он не нужен. Добавить его abstract'ом значит вернуть ту самую
//ломкость, от которой ушли.
//
//Сегодняшние 66 членов abstract намеренно: готовый набор реализаций лежит
//в DefaultInjector, и от него же и наследуются, когда нужно переопределить
//одну перегрузку, а не все 66.
//
//Второе правило - к производительности: генератор пишет вызов по конкретному
//типу, поэтому запечатанный наследник девиртуализуется и стоит ровно столько
//же, сколько невиртуальный метод (docs/dispatch-cost.md). Незапечатанный
//стоит дороже, и генератор об этом предупреждает.

using System;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe
{
    public abstract class InjectorBase : IInjector
    {
        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out DateTime value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out DateTime? value
            );

        public abstract void ParseBody(
            roschar body,
            out DateTime value
            );

        public abstract void ParseBody(
            roschar body,
            out DateTime? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out Guid value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out Guid? value
            );

        public abstract void ParseBody(
            roschar body,
            out Guid value
            );

        public abstract void ParseBody(
            roschar body,
            out Guid? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out bool value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out bool? value
            );

        public abstract void ParseBody(
            roschar body,
            out bool value
            );

        public abstract void ParseBody(
            roschar body,
            out bool? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out sbyte value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out sbyte? value
            );

        public abstract void ParseBody(
            roschar body,
            out sbyte value
            );

        public abstract void ParseBody(
            roschar body,
            out sbyte? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out byte value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out byte? value
            );

        public abstract void ParseBody(
            roschar body,
            out byte value
            );

        public abstract void ParseBody(
            roschar body,
            out byte? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out ushort value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out ushort? value
            );

        public abstract void ParseBody(
            roschar body,
            out ushort value
            );

        public abstract void ParseBody(
            roschar body,
            out ushort? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out short value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out short? value
            );

        public abstract void ParseBody(
            roschar body,
            out short value
            );

        public abstract void ParseBody(
            roschar body,
            out short? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out uint value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out uint? value
            );

        public abstract void ParseBody(
            roschar body,
            out uint value
            );

        public abstract void ParseBody(
            roschar body,
            out uint? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out int value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out int? value
            );

        public abstract void ParseBody(
            roschar body,
            out int value
            );

        public abstract void ParseBody(
            roschar body,
            out int? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out ulong value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out ulong? value
            );

        public abstract void ParseBody(
            roschar body,
            out ulong value
            );

        public abstract void ParseBody(
            roschar body,
            out ulong? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out long value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out long? value
            );

        public abstract void ParseBody(
            roschar body,
            out long value
            );

        public abstract void ParseBody(
            roschar body,
            out long? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out decimal value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out decimal? value
            );

        public abstract void ParseBody(
            roschar body,
            out decimal value
            );

        public abstract void ParseBody(
            roschar body,
            out decimal? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out float value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out float? value
            );

        public abstract void ParseBody(
            roschar body,
            out float value
            );

        public abstract void ParseBody(
            roschar body,
            out float? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out double value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out double? value
            );

        public abstract void ParseBody(
            roschar body,
            out double value
            );

        public abstract void ParseBody(
            roschar body,
            out double? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out char value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out char? value
            );

        public abstract void ParseBody(
            roschar body,
            out char value
            );

        public abstract void ParseBody(
            roschar body,
            out char? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out TimeSpan value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out TimeSpan? value
            );

        public abstract void ParseBody(
            roschar body,
            out TimeSpan value
            );

        public abstract void ParseBody(
            roschar body,
            out TimeSpan? value
            );

        public abstract void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out string value
            );

        public abstract void ParseBody(
            roschar body,
            out string value
            );
    }
}
