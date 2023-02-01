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


    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived1Info))]
    [XmlSubject(typeof(Derived1Info), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived2Info))]
    [XmlSubject(typeof(Derived2Info), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived3Info))]
    [XmlSubject(typeof(Derived3Info), false)]
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.PerformanceTests.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializer
    {
    }
}
