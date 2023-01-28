using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmlSerDe.Generator.EmbeddedCode;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests
{


    [XmlSubject(typeof(XmlObject1))] //[XmlSubject(typeof(XmlObject1))]
    public partial class XmlSerializerDeserializer1
    {
    }

    [XmlSubject(typeof(XmlObject2))] //[XmlSubject(typeof(XmlObject2))]
    public partial class XmlSerializerDeserializer2
    {
    }

    [XmlSubject(typeof(XmlObject2))] //[XmlSubject(typeof(XmlObject2))]
    [XmlSubject(typeof(XmlObject3))] //[XmlSubject(typeof(XmlObject3))]
    public partial class XmlSerializerDeserializer23
    {
    }

    [XmlSubject(typeof(XmlObject4Abstract))]
    [XmlDerivedSubject(typeof(XmlObject4Abstract), typeof(XmlObject4Specific1))]
    [XmlDerivedSubject(typeof(XmlObject4Abstract), typeof(XmlObject4Specific2))]
    [XmlSubject(typeof(XmlObject4Specific1))]
    [XmlSubject(typeof(XmlObject4Specific2))]
    [XmlSubject(typeof(XmlObject5))] //[XmlSubject(typeof(XmlObject5))]
    public partial class XmlSerializerDeserializer45
    {
    }

    [XmlSubject(typeof(XmlObject6))] //[XmlSubject(typeof(XmlObject6))]
    public partial class XmlSerializerDeserializer6
    {
    }

    [XmlSubject(typeof(XmlObject7))]
    [XmlSubject(typeof(XmlObject8))] //[XmlSubject(typeof(XmlObject8))]
    public partial class XmlSerializerDeserializer78
    {
    }

    [XmlSubject(typeof(XmlObject9Base))]
    [XmlDerivedSubject(typeof(XmlObject9Base), typeof(XmlObject9Specific1))]
    [XmlDerivedSubject(typeof(XmlObject9Base), typeof(XmlObject9Specific2))]
    [XmlSubject(typeof(XmlObject9Specific1))]
    [XmlSubject(typeof(XmlObject9Specific2))]
    [XmlSubject(typeof(XmlObject10))]
    public partial class XmlSerializerDeserializer910
    {
    }

    [XmlSubject(typeof(XmlObject11Abstract))]
    [XmlDerivedSubject(typeof(XmlObject11Abstract), typeof(XmlObject11Specific1))]
    [XmlDerivedSubject(typeof(XmlObject11Abstract), typeof(XmlObject11Specific2))]
    [XmlSubject(typeof(XmlObject11Specific1))]
    [XmlSubject(typeof(XmlObject11Specific2))]
    [XmlSubject(typeof(XmlObject12))] //[XmlSubject(typeof(XmlObject12))]
    public partial class XmlSerializerDeserializer1112
    {
    }

    [XmlSubject(typeof(XmlObject13))]
    public partial class XmlSerializerDeserializer13
    {
    }

}
