﻿#nullable disable

using System.Xml.Serialization;

namespace XmlSerDe.Tests.Complex.Subject
{
    public class Derived3Info : BaseInfo
    {
        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        [XmlIgnore]
        public override InfoTypeEnum InfoType => InfoTypeEnum.Derived3;


    }

}
