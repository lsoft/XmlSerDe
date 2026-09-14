using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests.Complex
{

    [XmlExhauster(typeof(LengthEstimatorExhauster))]
    [XmlExhauster(typeof(PooledCharExhauster))]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.Tests.Complex.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializer
    {
    }

    [XmlFeatures(XmlFeature.SystemXmlCompatible)]
    [XmlExhauster(typeof(LengthEstimatorExhauster))]
    [XmlExhauster(typeof(PooledCharExhauster))]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.Tests.Complex.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializerCompatible
    {
    }
}
