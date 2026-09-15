#nullable disable

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.BclPort.Subject
{
    /// <summary>
    /// <c>XML_TypeWithFieldsOrdered</c>. Порядок в атрибутах намеренно не совпадает
    /// с порядком объявления - иначе сериализатор, игнорирующий <c>Order</c> вовсе,
    /// прошёл бы тест по случайности. Два члена носят одно и то же имя элемента
    /// <c>strfld</c>, так что прочитать их правильно можно только по порядку.
    /// </summary>
    public class TypeWithFieldsOrdered
    {
        [XmlElement(Order = 2, ElementName = "strfld")]
        public string StringField2;

        [XmlElement(Order = 1)]
        public int IntField1;

        [XmlElement(Order = 0)]
        public int IntField2;

        [XmlElement(Order = 3, ElementName = "strfld")]
        public string StringField1;
    }

    /// <summary>
    /// <c>Xml_TypeWithBinaryProperty</c>. Один и тот же <c>byte[]</c> в двух
    /// лексических формах, заданных <c>DataType</c>: <c>hexBinary</c> и
    /// <c>base64Binary</c>.
    ///
    /// <c>DataType =</c> XmlSerDe не читает вовсе (README, Limitations), поэтому
    /// <c>BinaryHexContent</c> обязан разойтись: у нас он уйдёт в base64.
    /// Форма заведена затем, чтобы "молча игнорируется" было видно тестом.
    /// </summary>
    public class TypeWithBinaryProperty
    {
        [XmlElement(DataType = "hexBinary")]
        public byte[] BinaryHexContent { get; set; }

        [XmlElement(DataType = "base64Binary")]
        public byte[] Base64Content { get; set; }
    }

    /// <summary>
    /// <c>Xml_EnumFlags</c>. У первоисточника перечисление сериализуется корнем;
    /// у нас корень обязан быть классом, поэтому оно переехало в член. Значение
    /// <c>One | Four</c> ни одному объявленному члену не равно, и BCL пишет его
    /// списком имён через пробел - <c>One Four</c>.
    ///
    /// README прямо говорит, что <c>[Flags]</c> не поддержан и запасная ветка
    /// уходит в <c>Enum.ToString()</c>, который даёт <c>One, Four</c> - через
    /// запятую. Расхождение ожидается.
    /// </summary>
    [Flags]
    public enum EnumFlags
    {
        One = 0x01,
        Two = 0x02,
        Three = 0x04,
        Four = 0x08,
    }

    public class EnumFlagsSubject
    {
        public EnumFlags Value { get; set; }
    }

    /// <summary>
    /// <c>Xml_TypeWithDateTimePropertyAsXmlTime</c>: <c>[XmlText(DataType = "time")]</c>.
    /// BCL пишет только время суток (<c>15:15:26.9870000Z</c>) и теряет дату;
    /// XmlSerDe <c>DataType</c> не читает и напишет полный <c>dateTime</c>.
    /// </summary>
    public class TypeWithDateTimePropertyAsXmlTime
    {
        [XmlText(DataType = "time")]
        public DateTime Value { get; set; }
    }

    /// <summary>
    /// <c>Xml_TypeWithMismatchBetweenAttributeAndPropertyType</c>:
    /// <c>[DefaultValue(true)]</c> при члене типа <c>int</c>. Сопоставить такое
    /// умолчание с членом нельзя ни одной из сторон, поэтому атрибут пишется
    /// всегда - и это ровно то, что показывает эталон исходного теста
    /// (<c>IntValue="120"</c>).
    ///
    /// У нас это до недавнего времени было не расхождением, а CS0019:
    /// сравнение <c>int != true</c> не компилируется.
    /// </summary>
    [XmlRoot("RootElement")]
    public class TypeWithMismatchBetweenAttributeAndPropertyType
    {
        [DefaultValue(true)]
        [XmlAttribute("IntValue")]
        public int IntValue { get; set; } = 120;
    }
}
