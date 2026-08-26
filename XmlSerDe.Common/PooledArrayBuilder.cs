using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace XmlSerDe.Common
{
    /// <summary>
    /// Накопитель элементов для члена типа <c>T[]</c> или <c>List&lt;T&gt;</c>
    /// с сеттером. Заменяет собой связку <c>new List&lt;T&gt;()</c> +
    /// <c>ToArray()</c> (или рост списка 0→4→8): аллоцируется сам список,
    /// затем внутренний массив, затем ещё по массиву на каждое удвоение,
    /// и только потом финальный буфер. Замер на <c>int[]</c> давал от
    /// 3.1 до 4.75 размеров результата в зависимости от длины.
    ///
    /// Здесь промежуточные буферы берутся из <see cref="ArrayPool{T}"/> и
    /// возвращаются туда же, так что на выходе остаётся ровно одна
    /// аллокация - массив нужного размера (у списка - его внутренний).
    ///
    /// Это структура, и это важно: она живёт как локальная переменная
    /// генерируемого метода и не должна стоить ещё одного объекта в куче.
    /// Копировать её нельзя - копия разделит буфер с оригиналом.
    /// </summary>
    public struct PooledArrayBuilder<T>
    {
        /// <summary>
        /// Возвращать в пул с очисткой нужно только тогда, когда элементы
        /// могут удерживать ссылки: иначе пул будет держать живыми объекты,
        /// на которые уже никто не смотрит. Для примитивов и перечислений
        /// очистка - чистые потери, поэтому решение принимается один раз
        /// на тип, а не на каждый возврат.
        ///
        /// Проверка намеренно грубая (структура из одних чисел тоже попадёт
        /// под очистку): дешёвого способа спросить
        /// "содержит ли T ссылки" на netstandard2.0 нет, а ошибаться
        /// безопаснее в сторону лишней работы, чем в сторону утечки.
        /// </summary>
        private static readonly bool ClearOnReturn =
            !(typeof(T).IsPrimitive || typeof(T).IsEnum);

        /// <summary>
        /// Столько же, сколько берёт <see cref="System.Collections.Generic.List{T}"/>
        /// при первом <c>Add</c>, умноженное на два: пул всё равно округляет
        /// запрос вверх до степени двойки, так что меньшие значения ничего
        /// не экономят.
        /// </summary>
        private const int InitialCapacity = 8;

        private T[]? _buffer;
        private int _count;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            var buffer = _buffer;

            if (buffer is null)
            {
                buffer = ArrayPool<T>.Shared.Rent(InitialCapacity);
                _buffer = buffer;
            }
            else if (_count == buffer.Length)
            {
                buffer = Grow();
            }

            buffer[_count++] = item;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T[] Grow()
        {
            var previous = _buffer!;
            var bigger = ArrayPool<T>.Shared.Rent(previous.Length * 2);

            Array.Copy(previous, 0, bigger, 0, _count);
            ArrayPool<T>.Shared.Return(previous, ClearOnReturn);

            _buffer = bigger;
            return bigger;
        }

        /// <summary>
        /// Отдаёт накопленное ровно нужным по размеру массивом и возвращает
        /// буфер в пул. После вызова накопитель снова пуст.
        ///
        /// Если разбор оборвётся исключением между <see cref="Add"/> и этим
        /// вызовом, буфер в пул не вернётся - и это осознанно:
        /// <see cref="ArrayPool{T}"/> невозвращённые массивы допускает
        /// (просто выделит новый), а оборачивать каждый член генерируемого
        /// метода в try/finally ради пути, который и так заканчивается
        /// исключением, дороже, чем эта потеря.
        /// </summary>
        public T[] ToArrayAndRelease()
        {
            var buffer = _buffer;
            if (buffer is null)
            {
                //то же, что вернул бы List<T>.ToArray() на пустом списке
                return Array.Empty<T>();
            }

            var result = new T[_count];
            Array.Copy(buffer, 0, result, 0, _count);

            ArrayPool<T>.Shared.Return(buffer, ClearOnReturn);
            _buffer = null;
            _count = 0;

            return result;
        }

        /// <summary>
        /// То же для <c>List&lt;T&gt;</c>: один массив ровно на <see cref="Count"/>,
        /// без промежуточного 0→4→8.
        /// </summary>
        public List<T> ToListAndRelease()
        {
            var buffer = _buffer;
            if (buffer is null)
            {
                return new List<T>();
            }

            var count = _count;
            if (count == 0)
            {
                ArrayPool<T>.Shared.Return(buffer, ClearOnReturn);
                _buffer = null;
                _count = 0;
                return new List<T>();
            }
#if NET8_0_OR_GREATER
            var result = new List<T>(count);
            CollectionsMarshal.SetCount(result, count);
            buffer.AsSpan(0, count).CopyTo(CollectionsMarshal.AsSpan(result));
#else
            var result = new List<T>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(buffer[i]);
            }
#endif

            ArrayPool<T>.Shared.Return(buffer, ClearOnReturn);
            _buffer = null;
            _count = 0;

            return result;
        }
    }
}
