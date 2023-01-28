using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe.Generator.EmbeddedCode;
using XmlSerDe.PerformanceTests.Subject;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.PerformanceTests
{


    [XmlSubject(typeof(SerializeKeyValue))]
    [XmlSubject(typeof(PerformanceTime))]
    [XmlRoot(typeof(InfoContainer))]
    [XmlSubject(typeof(BaseInfo))]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived1Info))]
    [XmlSubject(typeof(Derived1Info))]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived2Info))]
    [XmlSubject(typeof(Derived2Info))]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived3Info))]
    [XmlSubject(typeof(Derived3Info))]
    public partial class XmlSerializerDeserializer
    {
    }
}
