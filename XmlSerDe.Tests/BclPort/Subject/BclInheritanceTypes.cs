#nullable disable

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.BclPort.Subject
{
    /// <summary>
    /// <c>Xml_HiddenDerivedFieldTest</c>. Наследник прячет оба члена базы через
    /// <c>new</c>, причём меняя их природу местами: свойство базы прячется полем
    /// наследника, поле базы - свойством. Имена элементов при этом различаются
    /// только регистром (<c>value</c> и <c>Value</c>), что для XML - два разных
    /// имени.
    ///
    /// Утверждение исходного теста жёсткое: после round-trip через статический
    /// тип базы заполнен ровно член наследника, а оба члена базы остаются null.
    /// То есть сериализатор обязан различать спрятанный и прячущий члены,
    /// а не писать один из них дважды.
    /// </summary>
    [XmlInclude(typeof(DerivedClass))]
    public class BaseClass
    {
        public string value { get; set; }
        public string Value;
    }

    public class DerivedClass : BaseClass
    {
        public new string value;
        public new string Value { get; set; }
    }

    /// <summary>
    /// <c>Xml_BaseClassAndDerivedClassWithSameProperty</c>. То же самое, но
    /// без полиморфизма: сериализуется сам наследник, и все четыре члена базы
    /// спрятаны одноимёнными членами наследника. Проверяется, что в документ
    /// уходят члены наследника, а члены базы после чтения остаются пустыми.
    ///
    /// Атрибуты <c>[DataMember]</c> первоисточника сняты: здесь они не значат
    /// ничего (это остаток от общих типов с <c>DataContractSerializer</c>),
    /// а ссылку на <c>System.Runtime.Serialization</c> потянули бы.
    /// </summary>
    public class BaseClassWithSamePropertyName
    {
        public string StringProperty;

        public int IntProperty;

        public DateTime DateTimeProperty;

        public List<string> ListProperty;
    }

    public class DerivedClassWithSameProperty : BaseClassWithSamePropertyName
    {
        public new string StringProperty;

        public new int IntProperty;

        public new DateTime DateTimeProperty;

        public new List<string> ListProperty;
    }
}
