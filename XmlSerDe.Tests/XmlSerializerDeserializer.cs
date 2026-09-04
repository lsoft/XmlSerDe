using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Complex.Subject;
//using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Tests
{
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
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
    public partial class XmlSerializerDeserializer2_3
    {
    }

    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    public partial class XmlSerializerDeserializer4_5
    {
    }

    [XmlSubject(typeof(XmlObject6), true)]
    public partial class XmlSerializerDeserializer6
    {
    }

    [XmlSubject(typeof(XmlObject7), false)]
    [XmlSubject(typeof(XmlObject8), true)]
    public partial class XmlSerializerDeserializer7_8
    {
    }

    [XmlSubject(typeof(XmlObject9Base), false)]
    [XmlSubject(typeof(XmlObject10), true)]
    public partial class XmlSerializerDeserializer9_10
    {
    }

    [XmlSubject(typeof(XmlObject11Abstract), false)]
    [XmlSubject(typeof(XmlObject12), true)]
    public partial class XmlSerializerDeserializer11_12
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

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterStream))]
    [XmlSubject(typeof(XmlObject16), true)]
    [XmlSubject(typeof(XmlObject17), true)]
    public partial class XmlSerializerDeserializer16_17
    {
    }

    [XmlSubject(typeof(XmlObject18), true)]
    public partial class XmlSerializerDeserializer18
    {
    }

    [XmlSubject(typeof(XmlObject19), true)]
    public partial class XmlSerializerDeserializer19
    {
    }

    [XmlSubject(typeof(XmlObject20), true)]
    public partial class XmlSerializerDeserializer20
    {
    }

    [XmlSubject(typeof(XmlObject21), true)]
    public partial class XmlSerializerDeserializer21
    {
    }

    [XmlSubject(typeof(XmlObject22), true)]
    public partial class XmlSerializerDeserializer22
    {
    }

    [XmlSubject(typeof(XmlObject23), true)]
    [XmlSubject(typeof(XmlObject24), false)]
    public partial class XmlSerializerDeserializer23_24
    {
    }

    [XmlSubject(typeof(XmlObject25), true)]
    [XmlSubject(typeof(XmlObject26), false)]
    public partial class XmlSerializerDeserializer25_26_27
    {
    }

    [XmlSubject(typeof(XmlObject28), true)]
    [XmlSubject(typeof(XmlObject29), false)]
    public partial class XmlSerializerDeserializer28_29_30
    {
    }

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlExhauster(typeof(LengthEstimatorExhauster))]
    [XmlExhauster(typeof(PooledCharExhauster))]
    [XmlSubject(typeof(XmlObject31), true)]
    public partial class XmlSerializerDeserializer31
    {
    }

    [XmlSubject(typeof(XmlObject32), true)]
    public partial class XmlSerializerDeserializer32
    {
    }

    [XmlSubject(typeof(XmlObject33), true)]
    [XmlFeatures(XmlFeature.CData)]
    public partial class XmlSerializerDeserializer33
    {
    }
}
