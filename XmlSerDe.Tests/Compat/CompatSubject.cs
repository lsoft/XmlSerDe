#nullable disable

using System;
using System.Collections.Generic;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Тип, который фасад умеет обслуживать быстрым путём.
    /// </summary>
    public class CompatSubject
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public List<int> Values { get; set; }
    }

    /// <summary>
    /// Тип, который в реестре не объявлен вовсе: на нём проверяется обещание
    /// "незарегистрированный тип не ломается, а просто не ускоряется".
    /// </summary>
    public class UnregisteredSubject
    {
        public int Number { get; set; }
        public string Name { get; set; }
    }

    //using System здесь обязателен - см. комментарий в Interop/InteropSerializer.cs
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlSubject(typeof(CompatSubject), true)]
    public partial class CompatSerializer
    {
        /// <summary>
        /// То, что в готовом виде будет порождать генератор по точке вызова
        /// <c>new XmlSerializer(typeof(T))</c>: пара делегатов в реестр.
        /// Пока это ручная регистрация - ровно затем, чтобы рантайм фасада можно
        /// было проверить отдельно от разбора call-site'ов.
        /// </summary>
        public static void Register()
        {
            XmlSerDe.Compat.XmlSerDeRegistry.Register<CompatSubject>(
                (exhauster, obj, appendXmlHead) => Serialize(
                    (DefaultStringBuilderExhauster)exhauster,
                    (CompatSubject)obj,
                    appendXmlHead
                    ),
                xml =>
                {
                    Deserialize(DefaultInjector.Instance, xml, out CompatSubject result);
                    return result;
                }
                );
        }
    }
}
