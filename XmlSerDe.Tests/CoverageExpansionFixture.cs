using System;
using System.Text;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Tests for coverage gaps: all primitive types, collections edge cases,
    /// length estimation, and CDATA strings.
    /// </summary>
    public class CoverageExpansionFixture
    {
        private static XmlObject31 CreateFullPrimitivesObject()
        {
            return new XmlObject31
            {
                BoolProperty = true,
                SByteProperty = -42,
                ByteProperty = 200,
                ShortProperty = -1234,
                UShortProperty = 65000,
                IntProperty = -987654,
                UIntProperty = 4000000000,
                LongProperty = -922337203685477580,
                ULongProperty = 18446744073709551615,
                DecimalProperty = 1234567890123456789012345.67m,
                DateTimeProperty = DateTime.Parse("2024-06-15T14:30:45.1234567+05:00"),
                GuidProperty = Guid.Parse("12345678-1234-5678-9abc-def012345678"),
                NullableBool = false,
                NullableInt = -7,
                NullableDecimal = 99.99m,
                NullableDateTime = DateTime.Parse("2020-01-01T00:00:00Z"),
                NullableGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
            };
        }

        [Fact]
        public void XmlObject31_AllPrimitives_Serialize_Test()
        {
            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer31.Serialize(sb, CreateFullPrimitivesObject(), false);
            var xml = sb.ToString();

            Assert.Contains("<BoolProperty>true</BoolProperty>", xml);
            Assert.Contains("<ByteProperty>200</ByteProperty>", xml);
            Assert.Contains("<DecimalProperty>", xml);
            Assert.Contains("<DateTimeProperty>", xml);
            Assert.Contains("<GuidProperty>", xml);
        }

        [Fact]
        public void XmlObject31_AllPrimitives_RoundTrip_Test()
        {
            var original = CreateFullPrimitivesObject();

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer31.Serialize(sb, original, false);
            XmlSerializerDeserializer31.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject31 result);

            Assert.Equal(original.BoolProperty, result.BoolProperty);
            Assert.Equal(original.SByteProperty, result.SByteProperty);
            Assert.Equal(original.ByteProperty, result.ByteProperty);
            Assert.Equal(original.ShortProperty, result.ShortProperty);
            Assert.Equal(original.UShortProperty, result.UShortProperty);
            Assert.Equal(original.IntProperty, result.IntProperty);
            Assert.Equal(original.UIntProperty, result.UIntProperty);
            Assert.Equal(original.LongProperty, result.LongProperty);
            Assert.Equal(original.ULongProperty, result.ULongProperty);
            Assert.Equal(original.DecimalProperty, result.DecimalProperty);
            Assert.Equal(original.DateTimeProperty, result.DateTimeProperty);
            Assert.Equal(original.GuidProperty, result.GuidProperty);
            Assert.Equal(original.NullableBool, result.NullableBool);
            Assert.Equal(original.NullableInt, result.NullableInt);
            Assert.Equal(original.NullableDecimal, result.NullableDecimal);
            Assert.Equal(original.NullableDateTime, result.NullableDateTime);
            Assert.Equal(original.NullableGuid, result.NullableGuid);
        }

        /// <summary>
        /// Null-члена значащего типа больше не пропадает: он пишется пустым элементом
        /// с xsi:nil="true". Отличать "члена не было" от "член был и равен default(T)"
        /// иначе нечем, и System.Xml.Serialization поступает так же.
        /// </summary>
        [Fact]
        public void XmlObject31_NullablePrimitives_WrittenAsNil_Test()
        {
            var obj = new XmlObject31
            {
                BoolProperty = false,
                IntProperty = 1
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer31.Serialize(sb, obj, false);
            var xml = sb.ToString();

            const string Nil = @" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:nil=""true"" />";

            Assert.Contains("<NullableBool" + Nil, xml);
            Assert.Contains("<NullableInt" + Nil, xml);
            Assert.Contains("<NullableDecimal" + Nil, xml);
            Assert.Contains("<NullableDateTime" + Nil, xml);
            Assert.Contains("<NullableGuid" + Nil, xml);

            //и своё же читается обратно в null
            XmlSerializerDeserializer31.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject31 result);

            Assert.Null(result.NullableBool);
            Assert.Null(result.NullableInt);
            Assert.Null(result.NullableDecimal);
            Assert.Null(result.NullableDateTime);
            Assert.Null(result.NullableGuid);
        }

        [Fact]
        public void XmlObject31_LengthEstimator_MatchesStringBuilder_Test()
        {
            var original = CreateFullPrimitivesObject();

            var estimator = new LengthEstimatorExhauster();
            XmlSerializerDeserializer31.Serialize(estimator, original, false);

            var sb = new StringBuilderExhauster(new StringBuilder(estimator.EstimatedTotalLength));
            XmlSerializerDeserializer31.Serialize(sb, original, false);

            Assert.True(
                sb.ToString().Length <= estimator.EstimatedTotalLength,
                $"Actual length {sb.ToString().Length} exceeded estimated length {estimator.EstimatedTotalLength}");
        }

        [Fact]
        public void XmlObject31_PooledChar_MatchesStringBuilder_Test()
        {
            var original = CreateFullPrimitivesObject();

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer31.Serialize(sb, original, false);

            var estimator = new LengthEstimatorExhauster();
            XmlSerializerDeserializer31.Serialize(estimator, original, false);

            using var pooled = new PooledCharExhauster(estimator.EstimatedTotalLength);
            XmlSerializerDeserializer31.Serialize(pooled, original, false);

            Assert.Equal(sb.ToString(), pooled.ToString());
            Assert.Equal(sb.ToString().Length, pooled.Written);
        }

        [Fact]
        public void XmlObject32_EmptyCollections_Serialize_Test()
        {
            var obj = new XmlObject32
            {
                EmptyList = new System.Collections.Generic.List<string>(),
                EmptyArray = Array.Empty<string>()
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer32.Serialize(sb, obj, false);
            var xml = sb.ToString();

            Assert.Contains("<EmptyList>", xml);
            Assert.Contains("<EmptyArray>", xml);
        }

        [Fact]
        public void XmlObject32_EmptyCollections_RoundTrip_Test()
        {
            var original = new XmlObject32
            {
                EmptyList = new System.Collections.Generic.List<string>(),
                EmptyArray = Array.Empty<string>()
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer32.Serialize(sb, original, false);
            XmlSerializerDeserializer32.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject32 result);

            Assert.NotNull(result.EmptyList);
            Assert.Empty(result.EmptyList);
            Assert.NotNull(result.EmptyArray);
            Assert.Empty(result.EmptyArray);
        }

        [Fact]
        public void XmlObject32_NullCollections_RoundTrip_Test()
        {
            var original = new XmlObject32
            {
                NullList = null,
                NullArray = null
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer32.Serialize(sb, original, false);
            XmlSerializerDeserializer32.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject32 result);

            Assert.Null(result.NullList);
            Assert.Null(result.NullArray);
        }

        [Fact]
        public void XmlObject33_CDataString_Deserialize_Test()
        {
            const string cdataContent = "raw <>&\"' content";
            var xml = $"<XmlObject33><CDataString><![CDATA[{cdataContent}]]></CDataString></XmlObject33>";

            XmlSerializerDeserializer33.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject33 result);

            Assert.Equal(cdataContent, result.CDataString);
        }

        [Fact]
        public void XmlObject33_CDataString_ConcatenatedBlocks_Deserialize_Test()
        {
            const string part1 = "first";
            const string part2 = "second";
            var xml = $"<XmlObject33><CDataString><![CDATA[{part1}]]><![CDATA[{part2}]]></CDataString></XmlObject33>";

            XmlSerializerDeserializer33.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject33 result);

            Assert.Equal(part1 + part2, result.CDataString);
        }

        [Fact]
        public void XmlObject33_PlainString_RoundTrip_Test()
        {
            var original = new XmlObject33
            {
                CDataString = SerDeFixture.RawString
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer33.Serialize(sb, original, false);
            XmlSerializerDeserializer33.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject33 result);

            Assert.Equal(original.CDataString, result.CDataString);
        }

        [Fact]
        public void XmlObject19_IntArray_SingleElement_RoundTrip_Test()
        {
            var original = new XmlObject19 { Ints = new[] { 42 } };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer19.Serialize(sb, original, false);
            XmlSerializerDeserializer19.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject19 result);

            Assert.Single(result.Ints);
            Assert.Equal(42, result.Ints[0]);
        }

        [Fact]
        public void XmlObject18_EnumList_SingleElement_RoundTrip_Test()
        {
            var original = new XmlObject18
            {
                Enums = new System.Collections.Generic.List<XmlEnum18> { XmlEnum18.B }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer18.Serialize(sb, original, false);
            XmlSerializerDeserializer18.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject18 result);

            Assert.Single(result.Enums);
            Assert.Equal(XmlEnum18.B, result.Enums[0]);
        }

        [Fact]
        public void XmlObject11_12_PolymorphicList_SecondDerived_RoundTrip_Test()
        {
            var original = new XmlObject12
            {
                XmlObjectProperty = new System.Collections.Generic.List<XmlObject11Abstract>
                {
                    new XmlObject11Specific2
                    {
                        StringProperty = "derived2",
                        IntProperty = 99
                    }
                }
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer11_12.Serialize(sb, original, false);
            XmlSerializerDeserializer11_12.Deserialize(
                DefaultInjector.Instance,
                sb.ToString().AsSpan(),
                out XmlObject12 result);

            var specific = Assert.IsType<XmlObject11Specific2>(result.XmlObjectProperty[0]);
            Assert.Equal("derived2", specific.StringProperty);
            Assert.Equal(99, specific.IntProperty);
        }

        [Fact]
        public void XmlObject2_Serialize_EncodedSpecialChars_Test()
        {
            var original = new XmlObject2
            {
                IntProperty = 0,
                StringProperty = SerDeFixture.RawString
            };

            var sb = new StringBuilderExhauster();
            XmlSerializerDeserializer2.Serialize(sb, original, false);
            var xml = sb.ToString();

            Assert.Contains(SerDeFixture.XmlEncodedString, xml);
            Assert.DoesNotContain("<StringProperty>" + SerDeFixture.RawString, xml);
        }
    }
}
