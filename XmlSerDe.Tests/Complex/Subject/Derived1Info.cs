#nullable disable

using System;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Complex.Subject
{
    public class Derived1Info : BaseInfo
    {
        public string BasePersonificationInfo { get; set; }

        [XmlIgnore]
        public override InfoTypeEnum InfoType
        {
            get
            {
                return InfoTypeEnum.Derived1;
            }
        }

    }

}
