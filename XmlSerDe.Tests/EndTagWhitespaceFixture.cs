using System;
using XmlSerDe;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// XML 1.0 §3.1: <c>ETag ::= '&lt;/' Name S? '&gt;'</c> - между именем и
    /// <c>&gt;</c> закрывающего тега допустимы пробелы. Сериализаторы так не
    /// пишут, но документ well-formed, и System.Xml его принимает. Раньше
    /// имя тега считалось до <c>&gt;</c> и включало пробел, а разбор тела
    /// скалярного члена требовал <c>&gt;</c> сразу после имени.
    /// </summary>
    public class EndTagWhitespaceFixture
    {
        [Theory]
        [InlineData("<XmlObject2><StringProperty>x</StringProperty ><IntProperty>1</IntProperty></XmlObject2>")]
        [InlineData("<XmlObject2><StringProperty>x</StringProperty><IntProperty>1</IntProperty ></XmlObject2>")]
        [InlineData("<XmlObject2><StringProperty>x</StringProperty><IntProperty>1</IntProperty></XmlObject2 >")]
        [InlineData("<XmlObject2><StringProperty>x</StringProperty\t\r\n><IntProperty>1</IntProperty\n></XmlObject2\r\n>")]
        public void DefaultHost_EndTagWithTrailingSpace_IsAccepted(string xml)
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject2 result
                );

            Assert.Equal("x", result.StringProperty);
            Assert.Equal(1, result.IntProperty);
        }

        /// <summary>
        /// Страж совпадения тегов сравнивает имена - пробел в имя входить не должен,
        /// иначе корректный документ отвергается как «чужой закрывающий тег».
        /// </summary>
        [Fact]
        public void MatchingEndTagsGuard_EndTagWithTrailingSpace_IsAccepted()
        {
            GuardMatchingEndTagsHost.Deserialize(
                DefaultInjector.Instance,
                ("<GuardSubject id=\"1\"><Title>t</Title ><Child><Name>c</Name ></Child >"
                + "<Items><GuardChild><Name>i</Name></GuardChild ></Items ></GuardSubject >").AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
            Assert.Equal("c", result.Child!.Name);
            Assert.Single(result.Items!);
        }

        /// <summary>
        /// Пробел разрешён только после имени, а не внутри него и не перед ним.
        /// </summary>
        [Theory]
        [InlineData("<XmlObject2><StringProperty>x</ StringProperty><IntProperty>1</IntProperty></XmlObject2>")]
        [InlineData("<XmlObject2><StringProperty>x</String Property><IntProperty>1</IntProperty></XmlObject2>")]
        public void DefaultHost_SpaceBeforeOrInsideEndTagName_StillThrows(string xml)
        {
            Assert.ThrowsAny<Exception>(
                () => XmlSerializerDeserializer2.Deserialize(
                    DefaultInjector.Instance,
                    xml.AsSpan(),
                    out XmlObject2 _
                    )
                );
        }
    }
}
