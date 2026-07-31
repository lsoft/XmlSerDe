using System;
using System.IO;
using System.Text;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Complex.Subject;
using Xunit;

namespace XmlSerDe.Tests.Complex
{
    /// <summary>
    /// V2 complex-scenario tests: XmlFactory reuse, length estimation,
    /// stream serialization, and XML head handling.
    /// </summary>
    [Collection(ComplexTestsCollection.Name)]
    public class ComplexFixtureV2
    {
        [Fact]
        public void XmlFactory_ReusesSameInstance_V2()
        {
            var xml = ComplexFixture.AuxXml.AsSpan();

            XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer first);
            XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer second);

            Assert.Same(first, second);
        }

        [Fact]
        public void Serialize_WithXmlHead_V2_DeserializesAfterCut()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer.Serialize(sb, ComplexFixture.DefaultObject, appendXmlHead: true);
            var xml = sb.ToString();

            Assert.StartsWith("<?xml version=\"1.0\"", xml);

            var body = global::XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(xml.AsSpan());
            XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, body, out InfoContainer result);

            Assert.NotNull(result);
            Assert.Equal(3, result.InfoCollection.Count);
        }

        [Fact]
        public void Serialize_Stream_V2_RoundTrip()
        {
            using var ms = new MemoryStream();
            var exhauster = new Utf8BinaryExhausterStream(ms);
            XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, false);

            var xml = Encoding.UTF8.GetString(ms.ToArray());
            XmlSerializerDeserializer.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out InfoContainer result);

            Assert.Equal(3, result.InfoCollection.Count);
            var derived3 = Assert.IsType<Derived3Info>(result.InfoCollection[0]);
            Assert.Equal("example@example.com", derived3.Email);
        }

        [Fact]
        public void Deserialize_CDataStrings_V2_PreservesContent()
        {
            var result = ComplexFixture.Deserialize_XmlSerDe(ComplexFixture.AuxXml.AsSpan());
            var derived1 = Assert.IsType<Derived1Info>(result.InfoCollection[1]);

            Assert.Equal(ComplexFixture.RawString, derived1.BasePersonificationInfo1);
            Assert.Equal(ComplexFixture.RawString + ComplexFixture.RawString, derived1.BasePersonificationInfo2);
        }

        [Fact]
        public void Serialize_RoundTrip_V2_MatchesSystemXmlShape()
        {
            var xmlSerDeXml = ComplexFixture.Serialize_XmlSerDe(ComplexFixture.DefaultObject);
            var roundTrip = ComplexFixture.Deserialize_XmlSerDe(xmlSerDeXml.AsSpan());
            var systemXml = ComplexFixture.Deserialize_SystemXml(ComplexFixture.AuxXml);

            Assert.Equal(systemXml.InfoCollection.Count, roundTrip.InfoCollection.Count);

            var sysDerived2 = (Derived2Info)systemXml.InfoCollection[2];
            var rtDerived2 = (Derived2Info)roundTrip.InfoCollection[2];
            Assert.Equal(sysDerived2.StepsCounter, rtDerived2.StepsCounter);
            Assert.Equal(sysDerived2.HotKeyUsed, rtDerived2.HotKeyUsed);
            Assert.Equal(sysDerived2.EventsTime.Count, rtDerived2.EventsTime.Count);
        }
    }
}
