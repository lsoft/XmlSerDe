﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Complex.Subject;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests.Complex
{

    [XmlExhauster(typeof(DefaultLengthEstimatorExhauster))]
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
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
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.Tests.Complex.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializer
    {
    }
}
