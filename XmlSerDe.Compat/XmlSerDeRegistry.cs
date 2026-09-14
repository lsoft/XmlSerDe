using System;
using System.Collections.Generic;
using XmlSerDe;
using XmlSerDe.Internal;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Пишет объект в сток. Экзостер приходит интерфейсом, а не своим типом:
    /// делегат должен подходить любому стоку, а какой именно тип ждёт
    /// сгенерированный метод, знает только тот, кто регистрируется, - он же
    /// и приводит.
    /// </summary>
    public delegate void CompatSerialize(IExhauster exhauster, object obj, bool appendXmlHead);

    /// <summary>
    /// Разбирает документ. На вход приходит уже тело: объявление
    /// &lt;?xml ...?&gt;, комментарии и прочий пролог срезаны
    /// (см. <see cref="XmlPrologue"/>), потому что забыть это сделать проще,
    /// чем помнить.
    /// </summary>
    public delegate object CompatDeserialize(ReadOnlySpan<char> xml);

    /// <summary>
    /// Пара делегатов, которой <see cref="XmlSerializer"/> обслуживает один тип.
    /// </summary>
    public readonly struct XmlSerDeEntry
    {
        public readonly CompatSerialize Serialize;
        public readonly CompatDeserialize Deserialize;

        /// <summary>
        /// Имя корневого элемента - то самое, которое пишет и ждёт сгенерированный
        /// код (<c>[XmlRoot]</c> перебивает имя типа). Лежит здесь ради
        /// <see cref="XmlSerializer.CanDeserialize"/>: вопрос «твой ли это документ»
        /// сводится к сравнению имени, и отвечать на него постройкой штатного
        /// сериализатора - платить рефлексией за одно сравнение строк.
        /// </summary>
        public readonly string RootElementName;

        public XmlSerDeEntry(
            CompatSerialize serialize,
            CompatDeserialize deserialize,
            string rootElementName
            )
        {
            Serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
            Deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));

            if (string.IsNullOrEmpty(rootElementName))
            {
                throw new ArgumentException($"'{nameof(rootElementName)}' cannot be null or empty.", nameof(rootElementName));
            }

            RootElementName = rootElementName;
        }
    }

    /// <summary>
    /// Типы, которые <see cref="XmlSerializer"/> умеет обслуживать быстрым путём.
    ///
    /// Заполняется на старте (генератор порождает <c>[ModuleInitializer]</c>), а
    /// дальше только читается, поэтому запись идёт копированием всего словаря под
    /// замком, а чтение - вовсе без синхронизации: читателю достаётся ссылка на
    /// словарь, который уже никто не меняет. Ценой одной аллокации на регистрацию
    /// это убирает блокировку с каждого вызова <c>Deserialize</c>.
    ///
    /// Незарегистрированный тип - не ошибка: <see cref="XmlSerializer"/> отдаёт его
    /// штатному <see cref="System.Xml.Serialization.XmlSerializer"/>. Именно поэтому
    /// генератор вправе промолчать на любом типе, который он не умеет.
    /// </summary>
    public static class XmlSerDeRegistry
    {
        private static readonly object _lock = new object();

        private static volatile Dictionary<Type, XmlSerDeEntry> _entries =
            new Dictionary<Type, XmlSerDeEntry>();

        /// <summary>
        /// Регистрирует тип. Повторная регистрация того же типа заменяет прежнюю:
        /// один и тот же тип вправе встретиться в нескольких сборках графа.
        /// </summary>
        public static void Register(
            Type type,
            CompatSerialize serialize,
            CompatDeserialize deserialize,
            string rootElementName
            )
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var entry = new XmlSerDeEntry(serialize, deserialize, rootElementName);

            lock (_lock)
            {
                var copy = new Dictionary<Type, XmlSerDeEntry>(_entries)
                {
                    [type] = entry
                };

                _entries = copy;
            }
        }

        public static void Register<T>(
            CompatSerialize serialize,
            CompatDeserialize deserialize,
            string rootElementName
            )
        {
            Register(typeof(T), serialize, deserialize, rootElementName);
        }

        public static bool TryGet(
            Type type,
            out XmlSerDeEntry entry
            )
        {
            return _entries.TryGetValue(type, out entry);
        }

        public static bool IsRegistered(Type type)
        {
            return _entries.ContainsKey(type);
        }
    }
}
