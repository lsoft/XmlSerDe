using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Пустой <see cref="XmlSerializationWriter"/>, нужный ровно затем, чтобы забрать
    /// у базового класса <see cref="XmlWriter"/> и список пространств имён.
    ///
    /// Это официальный контракт предгенерированных сборок сериализации: публичные
    /// <c>Serialize(...)</c> у <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// не виртуальные, но все они сходятся в <c>CreateWriter</c> +
    /// <c>Serialize(object, XmlSerializationWriter)</c>, а вот эти два - виртуальные.
    /// Через них фасад работает и тогда, когда его держат за базовый тип, - то есть
    /// в чужом коде, DI и полях существующих классов.
    /// </summary>
    internal sealed class XmlSerDeSerializationWriter : XmlSerializationWriter
    {
        public XmlWriter Target => Writer;

        /// <summary>
        /// То, что вызывающий передал как <see cref="XmlSerializerNamespaces"/>.
        /// XmlSerDe пишет пространства имён фиксированными литералами и чужие
        /// объявления учесть не может, поэтому непустой список - повод уйти
        /// в штатный сериализатор, а не выдать документ без них.
        /// </summary>
        public ArrayList? DeclaredNamespaces => Namespaces;

        protected override void InitCallbacks()
        {
            //коллбэки нужны только сгенерированному BCL коду, которого здесь нет
        }
    }
}
