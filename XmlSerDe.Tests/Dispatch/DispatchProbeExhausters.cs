//Экзостеры для DispatchMatrixFixture (--dispatch-cost): чего стоит форма
//наследника, когда контракт - базовый класс с virtual-методами.
//
//Внутри каждой семьи формы различаются ровно одним - тем, что JIT знает о типе
//на сгенерированном сайте вызова. Тела посимвольно одинаковы, поэтому разница
//во времени - это цена диспетчеризации и потерянного инлайна, и ничего больше.
//
//Семей две, и одна без другой врёт:
//
//  Thin - тело вырождено в одно сложение. Верхняя оценка: чем дешевле тело,
//         тем большую долю занимает вызов.
//  Fat  - тело то же, что у настоящего StringBuilderExhauster. Оценка для
//         живого кода.
//
//Формы «невиртуальный метод» здесь больше нет: ExhausterBase объявляет члены
//abstract, и реализация обязана быть override. Историческое сравнение с ней
//записано в docs/dispatch-cost.md - оно и показало, что запечатанный
//наследник неотличим от невиртуального метода.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using XmlSerDe.Common;

namespace XmlSerDe.Tests.Dispatch
{
    /// <summary>
    /// Вырожденное тело. Общая реализация: все члены переопределены здесь, наследники
    /// ниже отличаются только тем, запечатаны они или нет.
    /// </summary>
    public abstract class ThinBase : ExhausterBase
    {
        /// <summary>
        /// Сколько «написано». Читается после замера, чтобы вызовы нельзя было
        /// выкинуть как мёртвый код.
        /// </summary>
        public long Written;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value)
        {
            Written += value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncoded(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncodedUnchecked(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }

        #region заглушки: сгенерированный Serialize для XmlObject2 их не зовёт

        public override void Append(global::System.DateTime value) => throw new NotSupportedException();
        public override void Append(global::System.DateTime? value) => throw new NotSupportedException();
        public override void Append(global::System.Guid value) => throw new NotSupportedException();
        public override void Append(global::System.Guid? value) => throw new NotSupportedException();
        public override void Append(bool value) => throw new NotSupportedException();
        public override void Append(bool? value) => throw new NotSupportedException();
        public override void Append(sbyte value) => throw new NotSupportedException();
        public override void Append(sbyte? value) => throw new NotSupportedException();
        public override void Append(byte value) => throw new NotSupportedException();
        public override void Append(byte? value) => throw new NotSupportedException();
        public override void Append(ushort value) => throw new NotSupportedException();
        public override void Append(ushort? value) => throw new NotSupportedException();
        public override void Append(short value) => throw new NotSupportedException();
        public override void Append(short? value) => throw new NotSupportedException();
        public override void Append(uint value) => throw new NotSupportedException();
        public override void Append(uint? value) => throw new NotSupportedException();
        public override void Append(int? value) => throw new NotSupportedException();
        public override void Append(ulong value) => throw new NotSupportedException();
        public override void Append(ulong? value) => throw new NotSupportedException();
        public override void Append(long value) => throw new NotSupportedException();
        public override void Append(long? value) => throw new NotSupportedException();
        public override void Append(decimal value) => throw new NotSupportedException();
        public override void Append(decimal? value) => throw new NotSupportedException();
        public override void Append(float value) => throw new NotSupportedException();
        public override void Append(float? value) => throw new NotSupportedException();
        public override void Append(double value) => throw new NotSupportedException();
        public override void Append(double? value) => throw new NotSupportedException();
        public override void Append(char value) => throw new NotSupportedException();
        public override void Append(char? value) => throw new NotSupportedException();
        public override void Append(global::System.TimeSpan value) => throw new NotSupportedException();
        public override void Append(global::System.TimeSpan? value) => throw new NotSupportedException();
        public override void AppendAttributeEncoded(string? value) => throw new NotSupportedException();
        public override void AppendAttributeEncodedUnchecked(string? value) => throw new NotSupportedException();
        public override void AppendBase64(byte[]? value) => throw new NotSupportedException();

        #endregion
    }

    /// <summary>
    /// Вырожденное тело. Запечатанный наследник - рекомендуемая форма: точный тип
    /// известен статически, JIT девиртуализует вызов и инлайнит тело.
    /// </summary>
    public sealed class ThinSealedExhauster : ThinBase
    {
    }

    /// <summary>
    /// Вырожденное тело. Наследник открыт: тип в сигнатуре сгенерированного метода уже
    /// не гарантирует точный тип объекта. Ровно это получится у того, кто не
    /// написал sealed.
    /// </summary>
    public class ThinOpenExhauster : ThinBase
    {
    }

    /// <summary>
    /// Вырожденное тело. Второй наследник открытой ветки, со своим override. Нужен
    /// затем, что если через один и тот же сгенерированный вызов проходят два
    /// разных типа, сайт полиморфен, и угадывать там нечего - ни статически,
    /// ни по профилю. Заодно это и есть форма «запечатал и переопределил сам»,
    /// когда его зовут через собственный хост.
    /// </summary>
    public sealed class ThinSealedOverrideExhauster : ThinOpenExhauster
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value)
        {
            Written += value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncoded(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncodedUnchecked(string? value)
        {
            Written += value is null ? 0 : value.Length;
        }
    }

    /// <summary>
    /// Настоящее тело: запись в StringBuilder. Общая реализация: все члены переопределены здесь, наследники
    /// ниже отличаются только тем, запечатаны они или нет.
    /// </summary>
    public abstract class FatBase : ExhausterBase
    {
        protected readonly StringBuilder Sb = new StringBuilder(256);

        /// <summary>
        /// Проба зовёт это после каждого документа: иначе буфер растёт на все
        /// раунды. Метод невиртуальный во всех формах, поэтому в сравнение он
        /// входит одинаково.
        /// </summary>
        public void Clear()
        {
            Sb.Clear();
        }

        public int Length => Sb.Length;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value)
        {
            Sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncoded(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncodedUnchecked(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }

        #region заглушки: сгенерированный Serialize для XmlObject2 их не зовёт

        public override void Append(global::System.DateTime value) => throw new NotSupportedException();
        public override void Append(global::System.DateTime? value) => throw new NotSupportedException();
        public override void Append(global::System.Guid value) => throw new NotSupportedException();
        public override void Append(global::System.Guid? value) => throw new NotSupportedException();
        public override void Append(bool value) => throw new NotSupportedException();
        public override void Append(bool? value) => throw new NotSupportedException();
        public override void Append(sbyte value) => throw new NotSupportedException();
        public override void Append(sbyte? value) => throw new NotSupportedException();
        public override void Append(byte value) => throw new NotSupportedException();
        public override void Append(byte? value) => throw new NotSupportedException();
        public override void Append(ushort value) => throw new NotSupportedException();
        public override void Append(ushort? value) => throw new NotSupportedException();
        public override void Append(short value) => throw new NotSupportedException();
        public override void Append(short? value) => throw new NotSupportedException();
        public override void Append(uint value) => throw new NotSupportedException();
        public override void Append(uint? value) => throw new NotSupportedException();
        public override void Append(int? value) => throw new NotSupportedException();
        public override void Append(ulong value) => throw new NotSupportedException();
        public override void Append(ulong? value) => throw new NotSupportedException();
        public override void Append(long value) => throw new NotSupportedException();
        public override void Append(long? value) => throw new NotSupportedException();
        public override void Append(decimal value) => throw new NotSupportedException();
        public override void Append(decimal? value) => throw new NotSupportedException();
        public override void Append(float value) => throw new NotSupportedException();
        public override void Append(float? value) => throw new NotSupportedException();
        public override void Append(double value) => throw new NotSupportedException();
        public override void Append(double? value) => throw new NotSupportedException();
        public override void Append(char value) => throw new NotSupportedException();
        public override void Append(char? value) => throw new NotSupportedException();
        public override void Append(global::System.TimeSpan value) => throw new NotSupportedException();
        public override void Append(global::System.TimeSpan? value) => throw new NotSupportedException();
        public override void AppendAttributeEncoded(string? value) => throw new NotSupportedException();
        public override void AppendAttributeEncodedUnchecked(string? value) => throw new NotSupportedException();
        public override void AppendBase64(byte[]? value) => throw new NotSupportedException();

        #endregion
    }

    /// <summary>
    /// Настоящее тело: запись в StringBuilder. Запечатанный наследник - рекомендуемая форма: точный тип
    /// известен статически, JIT девиртуализует вызов и инлайнит тело.
    /// </summary>
    public sealed class FatSealedExhauster : FatBase
    {
    }

    /// <summary>
    /// Настоящее тело: запись в StringBuilder. Наследник открыт: тип в сигнатуре сгенерированного метода уже
    /// не гарантирует точный тип объекта. Ровно это получится у того, кто не
    /// написал sealed.
    /// </summary>
    public class FatOpenExhauster : FatBase
    {
    }

    /// <summary>
    /// Настоящее тело: запись в StringBuilder. Второй наследник открытой ветки, со своим override. Нужен
    /// затем, что если через один и тот же сгенерированный вызов проходят два
    /// разных типа, сайт полиморфен, и угадывать там нечего - ни статически,
    /// ни по профилю. Заодно это и есть форма «запечатал и переопределил сам»,
    /// когда его зовут через собственный хост.
    /// </summary>
    public sealed class FatSealedOverrideExhauster : FatOpenExhauster
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Append(int value)
        {
            Sb.Append(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncoded(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendEncodedUnchecked(string? value)
        {
            if (value is not null)
            {
                Sb.Append(value);
            }
        }
    }
}
