//the generator copies these usings into the generated file, so System is required here
using System;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep.Subject;

namespace XmlSerDe.Tests.Deep
{
    [XmlExhauster(typeof(LengthEstimatorExhauster))]
    [XmlExhauster(typeof(PooledCharExhauster))]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
    [XmlSubject(typeof(DeepNode), true)]
    public partial class DeepXmlSerializerDeserializer
    {
    }
}
