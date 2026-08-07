#nullable disable

namespace XmlSerDe.Tests.Complex.Subject
{
    [System.Xml.Serialization.XmlInclude(typeof(Derived1Info))]
    [System.Xml.Serialization.XmlInclude(typeof(Derived2Info))]
    [System.Xml.Serialization.XmlInclude(typeof(Derived3Info))]
    public abstract partial class BaseInfo
    {
        public abstract InfoTypeEnum InfoType
        {
            get;
        }
    }

}
