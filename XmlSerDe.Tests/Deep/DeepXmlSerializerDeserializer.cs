//the generator copies these usings into the generated file, so System is required here
using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Deep.Subject;

namespace XmlSerDe.Tests.Deep
{
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlSubject(typeof(DeepNode), true)]
    public partial class DeepXmlSerializerDeserializer
    {
    }
}
