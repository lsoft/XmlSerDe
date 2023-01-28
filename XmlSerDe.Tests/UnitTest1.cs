using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using XmlSerDe.Generator.EmbeddedCode;
using Xunit;

namespace XmlSerDe.Tests
{
    public class UnitTest1
    {
        public const string RawString = "my string !@#$%^&*()_+|-=\\';[]{},./<>?";
        //https://coderstoolbox.net/string/#!encoding=xml&action=encode&charset=us_ascii
        public const string XmlEncodedString = "my string !@#$%^&amp;*()_+|-=\\&#39;;[]{},./&lt;&gt;?";

        [Fact]
        public void XmlObject1_Test0()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1></XmlObject1>".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Test1()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1/>".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Test2()
        {
            XmlSerializerDeserializer1.Deserialize(@"<XmlObject1     />".AsSpan(), out XmlObject1 xo);
            Xunit.Assert.NotNull(xo);
        }

        [Fact]
        public void XmlObject1_Test3()
        {
            XmlSerializerDeserializer1.Deserialize(
                @"<object xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""XmlObject1""></object>".AsSpan(),
                out XmlObject1 xo
                );
            Xunit.Assert.NotNull(xo);
        }




        [Fact]
        public void XmlObject2_Test0()
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
        public void XmlObject2_Test1()
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
        public void XmlObject2_Test2()
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
        public void XmlObject2_Test3()
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
        public void XmlObject2_Test4()
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
        public void XmlObject23_Test0()
        {
            XmlSerializerDeserializer23.Deserialize(
                (@"<XmlObject3><XmlObjectProperty><IntProperty>123</IntProperty><StringProperty>" + XmlEncodedString + @"</StringProperty></XmlObjectProperty></XmlObject3>").AsSpan(),
                out XmlObject3 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObjectProperty);
            Xunit.Assert.Equal(123, xo.XmlObjectProperty.IntProperty);
            Xunit.Assert.Equal(RawString, xo.XmlObjectProperty.StringProperty);
        }





        [Fact]
        public void XmlObject45_Test0()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject5), new Type[] { typeof(XmlObject4Abstract), typeof(XmlObject4Specific2), typeof(XmlObject4Specific1) }).Serialize(
            //        ms,
            //        new XmlObject5()
            //        {
            //            XmlObjectProperty = new XmlObject4Specific1
            //            {
            //                StringProperty = "a",
            //                IntProperty = 123
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer45.Deserialize(
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
        public void XmlObject45_Test1()
        {
            //var ms = new MemoryStream();
            //new XmlSerializer(
            //    typeof(XmlObject5), new Type[] { typeof(XmlObject4Abstract), typeof(XmlObject4Specific2), typeof(XmlObject4Specific1) }).Serialize(
            //        ms,
            //        new XmlObject5()
            //        {
            //            XmlObjectProperty = new XmlObject4Specific1
            //            {
            //                StringProperty = "a",
            //                IntProperty = 123
            //            }
            //        }
            //    );
            //var q = Encoding.UTF8.GetString(ms.ToArray());

            XmlSerializerDeserializer45.Deserialize(
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
        public void XmlObject6_Test0()
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
        public void XmlObject78_Test0()
        {
            XmlSerializerDeserializer78.Deserialize(
                (@"<XmlObject8 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""><XmlObject7Property><StringProperty>a</StringProperty></XmlObject7Property></XmlObject8>").AsSpan(),
                out XmlObject8 xo
                );
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.NotNull(xo.XmlObject7Property);
            Xunit.Assert.Equal("a", xo.XmlObject7Property.StringProperty);
        }




        [Fact]
        public void XmlObject910_Test1()
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

            XmlSerializerDeserializer910.Deserialize(
                (@"<XmlObject10><XmlObjectProperty xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""XmlObject9Specific1"">  <StringProperty>a</StringProperty><IntProperty>123</IntProperty>  </XmlObjectProperty></XmlObject10>").AsSpan(),
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
        public void XmlObject1112_Test0()
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

            XmlSerializerDeserializer1112.Deserialize(
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
        public void XmlObject13_Test0()
        {
            XmlSerializerDeserializer13.Deserialize(@"<XmlObject13><IntProperty>123</IntProperty><StringProperty>a</StringProperty></XmlObject13>".AsSpan(), out XmlObject13 xo);
            Xunit.Assert.NotNull(xo);
            Xunit.Assert.Equal(0, xo.IntProperty);
            Xunit.Assert.Equal("a", xo.StringProperty);
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

        //namespace XmlSerDe.Tests
        //{
        //    [XmlSerDeSubject(typeof(XmlObject1))]
        //    public partial class XmlSerializerDeserializer
        //    {
        //    }

        //    public class XmlObject1
        //    {
        //    }
        //}
        //        ";

        //            var result = TestHelper.Verify(incoming);
        //        }


    }

}
