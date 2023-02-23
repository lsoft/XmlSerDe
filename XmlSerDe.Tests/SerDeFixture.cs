using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using XmlSerDe.Common;
using Xunit;

namespace XmlSerDe.Tests
{
    public class SerDeFixture
    {
        public const string RawString = "my string !@#$%^&*()_+|-=\\';[]{},./<>?";
        //https://coderstoolbox.net/string/#!encoding=xml&action=encode&charset=us_ascii
        public const string XmlEncodedString = "my string !@#$%^&amp;*()_+|-=\\&#39;;[]{},./&lt;&gt;?";

        [Fact]
        public void XmlObject1_Deserialize_Test0()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1></XmlObject1>".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer1.Serialize(
                sb,
                new XmlObject1(),
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal("<XmlObject1></XmlObject1>", xml);
        }

        [Fact]
        public void XmlObject1_SerializeStream_Test0()
        {
            using var ms = new MemoryStream();
            var be = new Utf8BinaryExhausterChild(ms);
            XmlSerializerDeserializer1.Serialize(
                be,
                new XmlObject1(),
                false
                );
            var xml = Encoding.UTF8.GetString(ms.ToArray());
            Xunit.Assert.Equal("<XmlObject1></XmlObject1>", xml);
        }

        [Fact]
        public void XmlObject1_Deserialize_Test0_WithHead()
        {
            XmlSerializerDeserializer1.Deserialize(
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    @"<?xml version=""1.0"" encoding=""utf-8""?><XmlObject1></XmlObject1>".AsSpan()
                    ), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Deserialize_Test1()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1/>".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Deserialize_Test2()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1     />".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Deserialize_Test3()
        {
            XmlSerializerDeserializer1.Deserialize(
                @"<object xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""XmlObject1""></object>".AsSpan(),
                out XmlObject1 xo
                );
            Xunit.Assert.NotNull(xo);
        }




        [Fact]
        public void XmlObject2_Deserialize_Test0()
        {
            XmlSerializerDeserializer2.Deserialize(
                @"<XmlObject2></XmlObject2>".AsSpan(),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(0, xo.IntProperty);
            Xunit.Assert.Null(xo.StringProperty);
        }

        [Fact]
        public void XmlObject2_Deserialize_Test1()
        {
            XmlSerializerDeserializer2.Deserialize(
                (@"<XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan(),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(123, xo.IntProperty);
            Xunit.Assert.Equal(string.Empty, xo.StringProperty);
        }

        [Fact]
        public void XmlObject2_Serialize_Test1()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer2.Serialize(
                sb,
                new XmlObject2
                {
                    IntProperty = 123,
                    StringProperty = "hello"
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject2><StringProperty>hello</StringProperty><IntProperty>123</IntProperty></XmlObject2>",
                xml
                );
        }

        [Fact]
        public void XmlObject2_Deserialize_Test1_WithHead()
        {
            XmlSerializerDeserializer2.Deserialize(
                XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    (@"<?xml version=""1.0"" encoding=""utf-8""?>    <XmlObject2><IntProperty>123</IntProperty><StringProperty></StringProperty></XmlObject2>").AsSpan()
                    ),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(123, xo.IntProperty);
            Xunit.Assert.Equal(string.Empty, xo.StringProperty);
        }

        [Fact]
        public void XmlObject2_Deserialize_Test2()
        {
            XmlSerializerDeserializer2.Deserialize(
                (@"<XmlObject2><IntProperty>123</IntProperty><StringProperty>" + XmlEncodedString + @"</StringProperty></XmlObject2>").AsSpan(),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(123, xo.IntProperty);
            Xunit.Assert.Equal(RawString, xo.StringProperty);
        }

        [Fact]
        public void XmlObject2_Deserialize_Test3()
        {
            XmlSerializerDeserializer2.Deserialize(
                (@"<object xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""XmlObject2""><IntProperty>123</IntProperty><StringProperty>" + XmlEncodedString + @"</StringProperty></object>").AsSpan(),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(123, xo.IntProperty);
            Xunit.Assert.Equal(RawString, xo.StringProperty);
        }

        [Fact]
        public void XmlObject2_Deserialize_Test4()
        {
            XmlSerializerDeserializer2.Deserialize(
                (@"<XmlObject2><IntProperty/><StringProperty/></XmlObject2>").AsSpan(),
                out XmlObject2 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(0, xo.IntProperty);
            Xunit.Assert.Null(xo.StringProperty);
        }





        [Fact]
        public void XmlObject2_3_Deserialize_Test0()
        {
            XmlSerializerDeserializer2_3.Deserialize(
                (@"<XmlObject3><XmlObjectProperty><IntProperty>123</IntProperty><StringProperty>" + XmlEncodedString + @"</StringProperty></XmlObjectProperty></XmlObject3>").AsSpan(),
                out XmlObject3 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            Xunit.Assert.Equal(123, xo.XmlObjectProperty.IntProperty);
            Xunit.Assert.Equal(RawString, xo.XmlObjectProperty.StringProperty);
        }





        [Fact]
        public void XmlObject4_5_Deserialize_Test0()
        {
            XmlSerializerDeserializer4_5.Deserialize(
                (@"<XmlObject5><XmlObjectProperty xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""XmlObject4Specific1""> <StringProperty>MyString</StringProperty><IntProperty>123</IntProperty>   </XmlObjectProperty></XmlObject5>").AsSpan(),
                out XmlObject5 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            var xo4specific = xo.XmlObjectProperty as XmlObject4Specific1;
            Xunit.Assert.NotNull(xo4specific);
            Xunit.Assert.Equal("MyString", xo4specific.StringProperty);
            Xunit.Assert.Equal(123, xo4specific.IntProperty);
        }

        [Fact]
        public void XmlObject4_5_Deserialize_Test1()
        {
            XmlSerializerDeserializer4_5.Deserialize(
                (@"<XmlObject5 xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance""><XmlObjectProperty p3:type=""XmlObject4Specific1""> <StringProperty>MyString</StringProperty><IntProperty>123</IntProperty>   </XmlObjectProperty></XmlObject5>").AsSpan(),
                out XmlObject5 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            var xo4specific = xo.XmlObjectProperty as XmlObject4Specific1;
            Xunit.Assert.NotNull(xo4specific);
            Xunit.Assert.Equal("MyString", xo4specific.StringProperty);
            Xunit.Assert.Equal(123, xo4specific.IntProperty);
        }




        [Fact]
        public void XmlObject6_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject6), new Type[] {  }).Serialize(
            //        ms,
            //        new XmlObject6()
            //        {
            //            StringsProperty = new List<string>
            //            {
            //                "a",
            //                "b"
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer6.Deserialize(
                (@"<XmlObject6><StringsProperty><string>a</string><string>b</string></StringsProperty></XmlObject6>").AsSpan(),
                out XmlObject6 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.StringsProperty);
            Xunit.Assert.Equal(2, xo.StringsProperty.Count);
            Xunit.Assert.Equal("a", xo.StringsProperty[0]);
            Xunit.Assert.Equal("b", xo.StringsProperty[1]);
        }

        [Fact]
        public void XmlObject6_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer6.Serialize(
                sb,
                new XmlObject6
                {
                    StringsProperty = new List<string>
                    {
                        "a",
                        "b"
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject6><StringsProperty><string>a</string><string>b</string></StringsProperty></XmlObject6>",
                xml
                );
        }


        [Fact]
        public void XmlObject7_8_Deserialize_Test0()
        {
            XmlSerializerDeserializer7_8.Deserialize(
                (@"<XmlObject8 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""><XmlObject7Property><StringProperty>a</StringProperty></XmlObject7Property></XmlObject8>").AsSpan(),
                out XmlObject8 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObject7Property);
            Xunit.Assert.Equal("a", xo.XmlObject7Property.StringProperty);
        }




        [Fact]
        public void XmlObject9_10_Deserialize_Test1()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject10), new Type[] { typeof(XmlObject9Base), typeof(XmlObject9Specific2), typeof(XmlObject9Specific1) }).Serialize(
            //        ms,
            //        new XmlObject10()
            //        {
            //            XmlObjectProperty = new XmlObject9Specific1
            //            {
            //                StringProperty = "a",
            //                IntProperty = 123
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer9_10.Deserialize(
                (@"   <XmlObject10>   <XmlObjectProperty xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject9Specific1"">  <StringProperty>a</StringProperty><IntProperty>123</IntProperty>  </XmlObjectProperty></XmlObject10>").AsSpan(),
                out XmlObject10 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            var xoSpecific = xo.XmlObjectProperty as XmlObject9Specific1;
            Xunit.Assert.NotNull(xoSpecific);
            Xunit.Assert.Equal("a", xoSpecific.StringProperty);
            Xunit.Assert.Equal(123, xoSpecific.IntProperty);
        }


        [Fact]
        public void XmlObject9_10_Serialize_Test1()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer9_10.Serialize(
                sb,
                new XmlObject10()
                {
                    XmlObjectProperty = new XmlObject9Specific1
                    {
                        StringProperty = "a",
                        IntProperty = 123
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject10><XmlObjectProperty xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject9Specific1""><IntProperty>123</IntProperty><StringProperty>a</StringProperty></XmlObjectProperty></XmlObject10>",
                xml
                );
        }





        [Fact]
        public void XmlObject11_12_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject12), new Type[] { typeof(XmlObject11Abstract), typeof(XmlObject11Specific2), typeof(XmlObject11Specific1) }).Serialize(
            //        ms,
            //        new XmlObject12()
            //        {
            //            XmlObjectProperty = new List<XmlObject11Abstract>
            //            {
            //                new XmlObject11Specific1
            //                {
            //                    StringProperty = "At0",
            //                    IntProperty = 0
            //                },
            //                new XmlObject11Specific1
            //                {
            //                    StringProperty = "At1",
            //                    IntProperty = 1
            //                },
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer11_12.Deserialize(
                (@"<XmlObject12><XmlObjectProperty><XmlObject11Abstract xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject11Specific1""><StringProperty>At0</StringProperty><IntProperty>0</IntProperty></XmlObject11Abstract><XmlObject11Abstract xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject11Specific1""><StringProperty>At1</StringProperty><IntProperty>1</IntProperty></XmlObject11Abstract></XmlObjectProperty></XmlObject12>").AsSpan(),
                out XmlObject12 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            Xunit.Assert.Equal(2, xo.XmlObjectProperty.Count);
            var xoSpecific1At0 = xo.XmlObjectProperty[0] as XmlObject11Specific1;
            Xunit.Assert.NotNull(xoSpecific1At0);
            Xunit.Assert.Equal("At0", xoSpecific1At0.StringProperty);
            Xunit.Assert.Equal(0, xoSpecific1At0.IntProperty);
            var xoSpecific1At1 = xo.XmlObjectProperty[1] as XmlObject11Specific1;
            Xunit.Assert.NotNull(xoSpecific1At1);
            Xunit.Assert.Equal("At1", xoSpecific1At1.StringProperty);
            Xunit.Assert.Equal(1, xoSpecific1At1.IntProperty);
        }


        [Fact]
        public void XmlObject11_12_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer11_12.Serialize(
                sb,
                new XmlObject12()
                {
                    XmlObjectProperty = new List<XmlObject11Abstract>
                    {
                        new XmlObject11Specific1
                        {
                            StringProperty = "At0",
                            IntProperty = 0
                        },
                        new XmlObject11Specific1
                        {
                            StringProperty = "At1",
                            IntProperty = 1
                        },
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject12><XmlObjectProperty><XmlObject11Abstract xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject11Specific1""><IntProperty>0</IntProperty><StringProperty>At0</StringProperty></XmlObject11Abstract><XmlObject11Abstract xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject11Specific1""><IntProperty>1</IntProperty><StringProperty>At1</StringProperty></XmlObject11Abstract></XmlObjectProperty></XmlObject12>",
                xml
                );
        }





        [Fact]
        public void XmlObject13_Deserialize_Test0()
        {
            XmlSerializerDeserializer13.Deserialize(@"<XmlObject13><IntProperty>123</IntProperty><StringProperty>a</StringProperty></XmlObject13>".AsSpan(), out XmlObject13 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(0, xo.IntProperty);
            Xunit.Assert.Equal("a", xo.StringProperty);
        }



        [Fact]
        public void XmlObject14_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject14), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject14()
            //        {
            //            NullableDateTime = null
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer14.Deserialize(@"<XmlObject14 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""><NullableDateTime xsi:nil=""true"" /><NullableGuid xsi:nil=""true"" /></XmlObject14>".AsSpan(), out XmlObject14 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Null(xo.NullableDateTime);
            Xunit.Assert.Null(xo.NullableGuid);
        }


        [Fact]
        public void XmlObject14_Deserialize_Test1()
        {
            var dt = new DateTime(638108474082389138);
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject14), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject14()
            //        {
            //            NullableDateTime = dt
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer14.Deserialize(@"<XmlObject14 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""><NullableDateTime>2023-02-01T11:23:28.2389138</NullableDateTime><NullableGuid>268450C0-C71B-4440-A04E-D83ABFBD9FAC</NullableGuid></XmlObject14>".AsSpan(), out XmlObject14 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(dt, xo.NullableDateTime);
            Xunit.Assert.Equal(new Guid("268450C0-C71B-4440-A04E-D83ABFBD9FAC"), xo.NullableGuid);
        }






        [Fact]
        public void XmlObject15_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject15), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject15()
            //        {
            //            XmlEnum15 = XmlEnum15.EnumValue1
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer15.Deserialize(@"<XmlObject15><XmlEnum15>EnumValue1</XmlEnum15></XmlObject15>".AsSpan(), out XmlObject15 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(XmlEnum15.EnumValue1, xo.XmlEnum15);
        }

        [Fact]
        public void XmlObject15_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer15.Serialize(
                sb,
                new XmlObject15()
                {
                    XmlEnum15 = XmlEnum15.EnumValue1
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject15><XmlEnum15>EnumValue1</XmlEnum15></XmlObject15>",
                xml
                );
        }



        [Fact]
        public void XmlObject16_17_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject17), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject17()
            //        {
            //            MyList = new List<XmlObject16>
            //            {
            //                new XmlObject16 { MyField = 1 },
            //                new XmlObject16 { MyField = 2 },
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer16_17.Deserialize(@"<XmlObject17><MyList><XmlObject16><MyField>1</MyField></XmlObject16><XmlObject16><MyField>2</MyField></XmlObject16></MyList></XmlObject17>".AsSpan(), out XmlObject17 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(2, xo.MyList.Count);
            Xunit.Assert.Equal(1, xo.MyList[0].MyField);
            Xunit.Assert.Equal(2, xo.MyList[1].MyField);
        }

        [Fact]
        public void XmlObject16_17_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer16_17.Serialize(
                sb,
                new XmlObject17()
                {
                    MyList = new List<XmlObject16>
                    {
                        new XmlObject16 { MyField = 1 },
                        new XmlObject16 { MyField = 2 },
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject17><MyList><XmlObject16><MyField>1</MyField></XmlObject16><XmlObject16><MyField>2</MyField></XmlObject16></MyList></XmlObject17>",
                xml
                );
        }

        [Fact]
        public void XmlObject16_17_SerializeStream_Test0()
        {
            using var ms = new MemoryStream();
            var be = new Utf8BinaryExhausterChild(ms);
            XmlSerializerDeserializer16_17.Serialize(
                be,
                new XmlObject17()
                {
                    MyList = new List<XmlObject16>
                    {
                        new XmlObject16 { MyField = 1 },
                        new XmlObject16 { MyField = 2 },
                    }
                },
                false
                );
            var xml = Encoding.UTF8.GetString(ms.ToArray());
            Xunit.Assert.Equal(
                @"<XmlObject17><MyList><XmlObject16><MyField>1</MyField></XmlObject16><XmlObject16><MyField>2</MyField></XmlObject16></MyList></XmlObject17>",
                xml
                );
        }



        [Fact]
        public void XmlObject18_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject18), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject18()
            //        {
            //            Enums = new List<XmlEnum18>
            //            {
            //                XmlEnum18.A,
            //                XmlEnum18.B,
            //                XmlEnum18.C,
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer18.Deserialize(@"<XmlObject18><Enums><XmlEnum18>A</XmlEnum18><XmlEnum18>B</XmlEnum18><XmlEnum18>C</XmlEnum18></Enums></XmlObject18>".AsSpan(), out XmlObject18 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Enums.Count);
            Xunit.Assert.Equal(XmlEnum18.A, xo.Enums[0]);
            Xunit.Assert.Equal(XmlEnum18.B, xo.Enums[1]);
            Xunit.Assert.Equal(XmlEnum18.C, xo.Enums[2]);
        }

        [Fact]
        public void XmlObject18_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer18.Serialize(
                sb,
                new XmlObject18()
                {
                    Enums = new List<XmlEnum18>
                    {
                        XmlEnum18.A,
                        XmlEnum18.B,
                        XmlEnum18.C,
                    }
                    },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject18><Enums><XmlEnum18>A</XmlEnum18><XmlEnum18>B</XmlEnum18><XmlEnum18>C</XmlEnum18></Enums></XmlObject18>",
                xml
                );
        }



        [Fact]
        public void XmlObject19_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject19), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject19()
            //        {
            //            Ints = new int[3]
            //            {
            //                -1,
            //                0,
            //                1
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer19.Deserialize(@"<XmlObject19><Ints><int>-1</int><int>0</int><int>1</int></Ints></XmlObject19>".AsSpan(), out XmlObject19 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Ints.Length);
            Xunit.Assert.Equal(-1, xo.Ints[0]);
            Xunit.Assert.Equal(0, xo.Ints[1]);
            Xunit.Assert.Equal(1, xo.Ints[2]);
        }

        [Fact]
        public void XmlObject19_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer19.Serialize(
                sb,
                new XmlObject19()
                {
                    Ints = new int[3]
                    {
                        -1,
                        0,
                        1
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject19><Ints><int>-1</int><int>0</int><int>1</int></Ints></XmlObject19>",
                xml
                );
        }


        [Fact]
        public void XmlObject20_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject20), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject20()
            //        {
            //            Bytes = new sbyte[3]
            //            {
            //                -1,
            //                0,
            //                1
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer20.Deserialize(@"<XmlObject20><Bytes><byte>-1</byte><byte>0</byte><byte>1</byte></Bytes></XmlObject20>".AsSpan(), out XmlObject20 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Bytes.Length);
            Xunit.Assert.Equal(-1, xo.Bytes[0]);
            Xunit.Assert.Equal(0, xo.Bytes[1]);
            Xunit.Assert.Equal(1, xo.Bytes[2]);
        }

        [Fact]
        public void XmlObject20_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer20.Serialize(
                sb,
                new XmlObject20()
                {
                    Bytes = new sbyte[3]
                    {
                        -1,
                        0,
                        1
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject20><Bytes><byte>-1</byte><byte>0</byte><byte>1</byte></Bytes></XmlObject20>",
                xml
                );
        }


        [Fact]
        public void XmlObject21_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject21), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject21()
            //        {
            //            Guids = new Guid[3]
            //            {
            //                Guid.Parse("7E6F7737-042D-4046-BF85-5596B47D6847"),
            //                Guid.Parse("E7EC0690-A00F-4F96-B772-A0529447121F"),
            //                Guid.Parse("3CD3F5B1-B364-4C30-8DC3-C7F9DFEAFB88"),
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer21.Deserialize(@"<XmlObject21><Guids><guid>7e6f7737-042d-4046-bf85-5596b47d6847</guid><guid>e7ec0690-a00f-4f96-b772-a0529447121f</guid><guid>3cd3f5b1-b364-4c30-8dc3-c7f9dfeafb88</guid></Guids></XmlObject21>".AsSpan(), out XmlObject21 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Guids.Length);
            Xunit.Assert.Equal(Guid.Parse("7E6F7737-042D-4046-BF85-5596B47D6847"), xo.Guids[0]);
            Xunit.Assert.Equal(Guid.Parse("E7EC0690-A00F-4F96-B772-A0529447121F"), xo.Guids[1]);
            Xunit.Assert.Equal(Guid.Parse("3CD3F5B1-B364-4C30-8DC3-C7F9DFEAFB88"), xo.Guids[2]);
        }

        [Fact]
        public void XmlObject21_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer21.Serialize(
                sb,
                new XmlObject21()
                {
                    Guids = new Guid[3]
                    {
                        Guid.Parse("7E6F7737-042D-4046-BF85-5596B47D6847"),
                        Guid.Parse("E7EC0690-A00F-4F96-B772-A0529447121F"),
                        Guid.Parse("3CD3F5B1-B364-4C30-8DC3-C7F9DFEAFB88"),
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject21><Guids><guid>7e6f7737-042d-4046-bf85-5596b47d6847</guid><guid>e7ec0690-a00f-4f96-b772-a0529447121f</guid><guid>3cd3f5b1-b364-4c30-8dc3-c7f9dfeafb88</guid></Guids></XmlObject21>",
                xml
                );
        }



        [Fact]
        public void XmlObject22_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject22), new Type[] { }).Serialize(
            //        ms,
            //        new XmlObject22()
            //        {
            //            Enums = new XmlEnum22[3]
            //            {
            //                XmlEnum22.A,
            //                XmlEnum22.B,
            //                XmlEnum22.C
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer22.Deserialize(@"<XmlObject22><Enums><XmlEnum22>A</XmlEnum22><XmlEnum22>B</XmlEnum22><XmlEnum22>C</XmlEnum22></Enums></XmlObject22>".AsSpan(), out XmlObject22 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Enums.Length);
            Xunit.Assert.Equal(XmlEnum22.A, xo.Enums[0]);
            Xunit.Assert.Equal(XmlEnum22.B, xo.Enums[1]);
            Xunit.Assert.Equal(XmlEnum22.C, xo.Enums[2]);
        }

        [Fact]
        public void XmlObject22_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer22.Serialize(
                sb,
                new XmlObject22()
                {
                    Enums = new XmlEnum22[3]
                    {
                        XmlEnum22.A,
                        XmlEnum22.B,
                        XmlEnum22.C
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject22><Enums><XmlEnum22>A</XmlEnum22><XmlEnum22>B</XmlEnum22><XmlEnum22>C</XmlEnum22></Enums></XmlObject22>",
                xml
                );
        }




        [Fact]
        public void XmlObject23_24_Deserialize_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject23), new Type[] { typeof(XmlObject24) }).Serialize(
            //        ms,
            //        new XmlObject23()
            //        {
            //            Objs = new XmlObject24[]
            //            {
            //                new XmlObject24(),
            //                new XmlObject24(),
            //                new XmlObject24()
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer23_24.Deserialize(@"<XmlObject23><Objs><XmlObject24></XmlObject24><XmlObject24></XmlObject24><XmlObject24></XmlObject24></Objs></XmlObject23>".AsSpan(), out XmlObject23 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(3, xo.Objs.Length);
            Xunit.Assert.NotNull(xo.Objs[0]);
            Xunit.Assert.NotNull(xo.Objs[1]);
            Xunit.Assert.NotNull(xo.Objs[2]);
        }

        [Fact]
        public void XmlObject23_24_Serialize_Test0()
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer23_24.Serialize(
                sb,
                new XmlObject23()
                {
                    Objs = new XmlObject24[]
                    {
                        new XmlObject24(),
                        new XmlObject24(),
                        new XmlObject24()
                    }
                },
                false
                );
            var xml = sb.ToString();
            Xunit.Assert.Equal(
                @"<XmlObject23><Objs><XmlObject24></XmlObject24><XmlObject24></XmlObject24><XmlObject24></XmlObject24></Objs></XmlObject23>",
                xml
                );
        }



        //        [Fact]
        //        public void TestGenerator()
        //        {
        //            var incoming = @"
        //        using System;
        //namespace XmlSerDe.Generator.EmbeddedCode
        //{
        //    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        //    public class XmlSerDeSubjectAttribute : Attribute
        //    {
        //        public readonly Type SerializationSubjectType;

        //        public XmlSerDeSubjectAttribute(Type serializationSubjectType)
        //        {
        //            SerializationSubjectType = serializationSubjectType;
        //        }
        //    }
        //}

    }

}
