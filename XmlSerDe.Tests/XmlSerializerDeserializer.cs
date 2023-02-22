using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe.Common;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests
{
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterChild))]
    [XmlSubject(typeof(XmlObject1), true)]
    public partial class XmlSerializerDeserializer1
    {
    }

    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializer2
    {
    }

    [XmlSubject(typeof(XmlObject2), false)]
    [XmlSubject(typeof(XmlObject3), true)]
    public partial class XmlSerializerDeserializer23
    {
    }

    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlDerivedSubject(typeof(XmlObject4Abstract), typeof(XmlObject4Specific1))]
    [XmlDerivedSubject(typeof(XmlObject4Abstract), typeof(XmlObject4Specific2))]
    [XmlSubject(typeof(XmlObject4Specific1), false)]
    [XmlSubject(typeof(XmlObject4Specific2), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    public partial class XmlSerializerDeserializer45
    {
    }

    [XmlSubject(typeof(XmlObject6), true)]
    public partial class XmlSerializerDeserializer6
    {
    }

    [XmlSubject(typeof(XmlObject7), false)]
    [XmlSubject(typeof(XmlObject8), true)]
    public partial class XmlSerializerDeserializer78
    {
    }

    [XmlSubject(typeof(XmlObject9Base), false)]
    [XmlDerivedSubject(typeof(XmlObject9Base), typeof(XmlObject9Specific1))]
    [XmlDerivedSubject(typeof(XmlObject9Base), typeof(XmlObject9Specific2))]
    [XmlSubject(typeof(XmlObject9Specific1), false)]
    [XmlSubject(typeof(XmlObject9Specific2), false)]
    [XmlSubject(typeof(XmlObject10), true)]
    public partial class XmlSerializerDeserializer910
    {
    }

    [XmlSubject(typeof(XmlObject11Abstract), false)]
    [XmlDerivedSubject(typeof(XmlObject11Abstract), typeof(XmlObject11Specific1))]
    [XmlDerivedSubject(typeof(XmlObject11Abstract), typeof(XmlObject11Specific2))]
    [XmlSubject(typeof(XmlObject11Specific1), false)]
    [XmlSubject(typeof(XmlObject11Specific2), false)]
    [XmlSubject(typeof(XmlObject12), true)]
    public partial class XmlSerializerDeserializer1112
    {
    }

    [XmlSubject(typeof(XmlObject13), true)]
    public partial class XmlSerializerDeserializer13
    {
    }

    [XmlSubject(typeof(XmlObject14), true)]
    public partial class XmlSerializerDeserializer14
    {
    }

    [XmlSubject(typeof(XmlObject15), true)]
    public partial class XmlSerializerDeserializer15
    {
    }

    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterChild))]
    [XmlSubject(typeof(XmlObject16), true)]
    [XmlSubject(typeof(XmlObject17), true)]
    public partial class XmlSerializerDeserializer1617
    {
    }
}
