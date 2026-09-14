using System;
using System.IO;
using System.Text;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// V2 variants of existing SerDeFixture tests — focused on round-trip fidelity
    /// and behaviors not asserted in the original tests.
    /// </summary>
    public class SerDeFixtureV2
    {
        public const string RawString = SerDeFixture.RawString;
        public const string XmlEncodedString = SerDeFixture.XmlEncodedString;

        [Fact]
        public void XmlObject2_Serialize_Test1_V2_RoundTrip()
        {
            var original = new XmlObject2
            {
                IntProperty = 123,
                StringProperty = "hello"
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer2.Serialize(sb, original, false);
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject2 result);

            Assert.Equal(original.IntProperty, result.IntProperty);
            Assert.Equal(original.StringProperty, result.StringProperty);
        }

        [Fact]
        public void XmlObject2_Deserialize_Test3_V2_RoundTripWithEncodedString()
        {
            var original = new XmlObject2
            {
                IntProperty = 123,
                StringProperty = RawString
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer2.Serialize(sb, original, false);
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject2 result);

            Assert.Equal(original.IntProperty, result.IntProperty);
            Assert.Equal(original.StringProperty, result.StringProperty);
        }

        [Fact]
        public void XmlObject2_3_Deserialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject3
            {
                XmlObjectProperty = new XmlObject2
                {
                    IntProperty = 123,
                    StringProperty = RawString
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer2_3.Serialize(sb, original, false);
            XmlSerializerDeserializer2_3.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject3 result);

            Assert.NotNull(result.XmlObjectProperty);
            Assert.Equal(123, result.XmlObjectProperty.IntProperty);
            Assert.Equal(RawString, result.XmlObjectProperty.StringProperty);
        }

        [Fact]
        public void XmlObject4_5_Deserialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject5
            {
                XmlObjectProperty = new XmlObject4Specific1
                {
                    StringProperty = "MyString",
                    IntProperty = 123
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer4_5.Serialize(sb, original, false);
            XmlSerializerDeserializer4_5.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject5 result);

            var specific = Assert.IsType<XmlObject4Specific1>(result.XmlObjectProperty);
            Assert.Equal("MyString", specific.StringProperty);
            Assert.Equal(123, specific.IntProperty);
        }

        [Fact]
        public void XmlObject4_5_Deserialize_Test1_V2_RoundTripSecondDerived()
        {
            var original = new XmlObject5
            {
                XmlObjectProperty = new XmlObject4Specific2
                {
                    StringProperty = "OtherString",
                    IntProperty = 456
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer4_5.Serialize(sb, original, false);
            XmlSerializerDeserializer4_5.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject5 result);

            var specific = Assert.IsType<XmlObject4Specific2>(result.XmlObjectProperty);
            Assert.Equal("OtherString", specific.StringProperty);
            Assert.Equal(456, specific.IntProperty);
        }

        [Fact]
        public void XmlObject6_Serialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject6
            {
                StringsProperty = new System.Collections.Generic.List<string>
                {
                    "alpha",
                    "beta",
                    RawString
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer6.Serialize(sb, original, false);
            XmlSerializerDeserializer6.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject6 result);

            Assert.Equal(original.StringsProperty.Count, result.StringsProperty.Count);
            Assert.Equal(original.StringsProperty[0], result.StringsProperty[0]);
            Assert.Equal(original.StringsProperty[1], result.StringsProperty[1]);
            Assert.Equal(original.StringsProperty[2], result.StringsProperty[2]);
        }

        [Fact]
        public void XmlObject7_8_Deserialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject8
            {
                XmlObject7Property = new XmlObject7
                {
                    StringProperty = RawString
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer7_8.Serialize(sb, original, false);
            XmlSerializerDeserializer7_8.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject8 result);

            Assert.NotNull(result.XmlObject7Property);
            Assert.Equal(RawString, result.XmlObject7Property.StringProperty);
        }

        [Fact]
        public void XmlObject9_10_Serialize_Test1_V2_RoundTrip()
        {
            var original = new XmlObject10
            {
                XmlObjectProperty = new XmlObject9Specific1
                {
                    StringProperty = "polymorphic",
                    IntProperty = 42
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer9_10.Serialize(sb, original, false);
            XmlSerializerDeserializer9_10.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject10 result);

            var specific = Assert.IsType<XmlObject9Specific1>(result.XmlObjectProperty);
            Assert.Equal("polymorphic", specific.StringProperty);
            Assert.Equal(42, specific.IntProperty);
        }

        [Fact]
        public void XmlObject13_Deserialize_Test0_V2_SerializeExcludesIgnoredProperty()
        {
            var original = new XmlObject13
            {
                IntProperty = 999,
                StringProperty = "visible"
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer13.Serialize(sb, original, false);
            var xml = sb.ToString();

            Assert.DoesNotContain("IntProperty", xml);
            Assert.Contains("visible", xml);

            XmlSerializerDeserializer13.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject13 result);

            Assert.Equal("visible", result.StringProperty);
            Assert.Equal(0, result.IntProperty);
        }

        [Fact]
        public void XmlObject14_Deserialize_Test0_V2_RoundTripWithNullables()
        {
            var original = new XmlObject14
            {
                NullableDateTime = DateTime.Parse("2023-01-15T10:30:00+03:00"),
                NullableGuid = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890")
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer14.Serialize(sb, original, false);
            XmlSerializerDeserializer14.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject14 result);

            Assert.Equal(original.NullableDateTime, result.NullableDateTime);
            Assert.Equal(original.NullableGuid, result.NullableGuid);
        }

        [Fact]
        public void XmlObject14_Deserialize_Test1_V2_RoundTripWithNullValues()
        {
            var original = new XmlObject14
            {
                NullableDateTime = null,
                NullableGuid = null
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer14.Serialize(sb, original, false);
            var xml = sb.ToString();

            //null-член теперь не пропадает, а превращается в пустой элемент с
            //xsi:nil="true" - ровно так же, как это делает System.Xml.Serialization
            Assert.Contains(@"<NullableDateTime xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:nil=""true"" />", xml);
            Assert.Contains(@"<NullableGuid xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:nil=""true"" />", xml);

            XmlSerializerDeserializer14.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject14 result);

            Assert.Null(result.NullableDateTime);
            Assert.Null(result.NullableGuid);
        }

        [Fact]
        public void XmlObject15_Serialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject15 { XmlEnum15 = XmlEnum15.EnumValue1 };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer15.Serialize(sb, original, false);
            XmlSerializerDeserializer15.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject15 result);

            Assert.Equal(XmlEnum15.EnumValue1, result.XmlEnum15);
        }

        [Fact]
        public void XmlObject1_Serialize_Test0_V2_WithXmlHead()
        {
            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer1.Serialize(sb, new XmlObject1(), appendXmlHead: true);
            var xml = sb.ToString();

            Assert.StartsWith("<?xml version=\"1.0\"", xml);
            Assert.Contains("<XmlObject1>", xml);

            var body = XmlSerDe.Internal.BuiltinCodeHelper.CutXmlHead(xml.AsSpan());
            XmlSerializerDeserializer1.Deserialize(
                DefaultInjector.Instance,
                body,
                out XmlObject1 result);

            Assert.NotNull(result);
        }

        [Fact]
        public void XmlObject1_SerializeStream_Test0_V2_RoundTrip()
        {
            using var ms = new MemoryStream();
            using (var be = new Utf8BinaryExhausterStream(ms))
            {
                XmlSerializerDeserializer1.Serialize(be, new XmlObject1(), false);
            }

            var xml = Encoding.UTF8.GetString(ms.ToArray());
            XmlSerializerDeserializer1.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject1 result);

            Assert.NotNull(result);
            Assert.Equal("<XmlObject1></XmlObject1>", xml);
        }

        [Fact]
        public void XmlObject16_17_Serialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject17
            {
                MyList = new System.Collections.Generic.List<XmlObject16>
                {
                    new XmlObject16 { MyField = 1 },
                    new XmlObject16 { MyField = 2 }
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer16_17.Serialize(sb, original, false);
            XmlSerializerDeserializer16_17.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject17 result);

            Assert.Equal(2, result.MyList.Count);
            Assert.Equal(1, result.MyList[0].MyField);
            Assert.Equal(2, result.MyList[1].MyField);
        }

        [Fact]
        public void XmlObject25_26_27_Serialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject25
            {
                Prop25 = new XmlObject27
                {
                    Prop26 = 26,
                    Prop27 = 27
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer25_26_27.Serialize(sb, original, false);
            XmlSerializerDeserializer25_26_27.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject25 result);

            var derived = Assert.IsType<XmlObject27>(result.Prop25);
            Assert.Equal(26, derived.Prop26);
            Assert.Equal(27, derived.Prop27);
        }

        [Fact]
        public void XmlObject28_29_30_Serialize_Test0_V2_RoundTrip()
        {
            var original = new XmlObject28
            {
                Prop28 = new System.Collections.Generic.List<XmlObject29>
                {
                    new XmlObject30 { Prop29 = 129, Prop30 = 130 },
                    new XmlObject30 { Prop29 = 229, Prop30 = 230 }
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer28_29_30.Serialize(sb, original, false);
            XmlSerializerDeserializer28_29_30.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject28 result);

            Assert.Equal(2, result.Prop28.Count);
            var first = Assert.IsType<XmlObject30>(result.Prop28[0]);
            Assert.Equal(129, first.Prop29);
            Assert.Equal(130, first.Prop30);
        }
    }
}
