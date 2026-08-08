#if NETSTANDARD
using System;
using System.Collections;
using System.Collections.Generic;

namespace XmlSerDe.Generator.Incremental
{
    /// <summary>
    /// Массив, который можно класть в инкрементальный конвейер.
    ///
    /// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> сравнивается по
    /// ссылке на внутренний массив, поэтому после каждого <c>Collect()</c> кэш
    /// драйвера считает значение новым, даже когда состав не изменился ни на йоту.
    /// Здесь равенство поэлементное - ради этого всё и заведено.
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(new T[0]);

        private readonly T[]? _items;

        public EquatableArray(T[] items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _items = items;
        }

        public int Count => _items is null ? 0 : _items.Length;

        public T this[int index] => _items![index];

        public bool Equals(EquatableArray<T> other)
        {
            var left = _items ?? Empty._items!;
            var right = other._items ?? Empty._items!;

            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var result = 17;

            if (_items is not null)
            {
                foreach (var item in _items)
                {
                    unchecked
                    {
                        result = (result * 31) + (item is null ? 0 : item.GetHashCode());
                    }
                }
            }

            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var items = _items ?? Empty._items!;
            foreach (var item in items)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class EquatableArray
    {
        public static EquatableArray<T> From<T>(List<T> items)
            where T : IEquatable<T>
        {
            return items.Count == 0
                ? EquatableArray<T>.Empty
                : new EquatableArray<T>(items.ToArray());
        }
    }
}
#endif
