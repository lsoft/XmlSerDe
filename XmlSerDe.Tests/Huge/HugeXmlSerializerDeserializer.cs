//the generator copies these usings into the generated file, so System is required here
using System;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Huge.Subject;

namespace XmlSerDe.Tests.Huge
{
    [XmlExhauster(typeof(LengthEstimatorExhauster))]
    [XmlExhauster(typeof(PooledCharExhauster))]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
    [XmlSubject(typeof(HugeDocument), true)]
    [XmlSubject(typeof(HugeRecord), false)]
    [XmlSubject(typeof(HugeScalars), false)]
    [XmlSubject(typeof(HugeNullables), false)]
    [XmlSubject(typeof(HugeStrings), false)]
    [XmlSubject(typeof(HugeNode), false)]
    [XmlSubject(typeof(HugeChild), false)]
    [XmlSubject(typeof(HugeShape), false)]
    [XmlSubject(typeof(HugeBase), false)]
    [XmlSubject(typeof(HugeTextPayload), false)]
    [XmlSubject(typeof(HugeOrdered), false)]
    [XmlSubject(typeof(HugeEmpty), false)]
    public partial class HugeXmlSerializerDeserializer
    {
    }
}
