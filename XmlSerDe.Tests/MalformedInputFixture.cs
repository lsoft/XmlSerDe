using System;
using System.Collections.Generic;
using XmlSerDe.Components.Injector;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Что происходит на документе, который никто не обещал делать корректным.
    ///
    /// Разбор span-ом устроен на арифметике по индексам, и у неё есть свойство,
    /// которого нет у разбора через <see cref="System.Xml.XmlReader"/>: ошибка в
    /// вычислении границы не превращается в «документ битый», она превращается
    /// в <see cref="IndexOutOfRangeException"/>, в <see cref="ArgumentOutOfRangeException"/>
    /// или в молча срезанный не там кусок. Первое и второе - это диагностика,
    /// которую невозможно ни поймать по смыслу, ни отличить от собственной ошибки
    /// вызывающего; третье хуже обоих.
    ///
    /// Поэтому здесь проверяется ровно одно свойство, зато на большом числе входов:
    /// <b>разбор битого документа заканчивается либо результатом, либо осмысленным
    /// исключением</b>. Осмысленное - это <see cref="InvalidOperationException"/>
    /// (так о битом документе говорит и сам XmlSerDe, и, завернув чужое,
    /// <see cref="System.Xml.Serialization.XmlSerializer"/>) либо
    /// <see cref="FormatException"/> с <see cref="OverflowException"/> - лексема
    /// на месте, но числом не является.
    ///
    /// Корпус строится из правильных документов механически: все префиксы, все
    /// удаления одного символа. Это дёшево, это воспроизводимо, и это находит
    /// ровно тот класс ошибок, ради которого написано, - руками такие входы
    /// не придумываются.
    /// </summary>
    public class MalformedInputFixture
    {
        private const string Simple =
            "<XmlObject2><IntProperty>123</IntProperty><StringProperty>text</StringProperty></XmlObject2>";

        private const string WithAttributes =
            @"<XmlObject5><XmlObjectProperty xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""XmlObject4Specific1""><StringProperty>MyString</StringProperty><IntProperty>123</IntProperty></XmlObjectProperty></XmlObject5>";

        private const string WithCollection =
            "<XmlObject17><MyList><XmlObject16><MyField>1</MyField></XmlObject16><XmlObject16><MyField>2</MyField></XmlObject16></MyList></XmlObject17>";

        [Fact]
        public void TruncatedDocument_NeverThrowsBoundsError_Test()
        {
            foreach (var (xml, parse) in Subjects())
            {
                for (var length = 0; length <= xml.Length; length++)
                {
                    AssertSaneOutcome(xml.Substring(0, length), parse);
                }
            }
        }

        [Fact]
        public void SingleCharacterDeletion_NeverThrowsBoundsError_Test()
        {
            foreach (var (xml, parse) in Subjects())
            {
                for (var position = 0; position < xml.Length; position++)
                {
                    AssertSaneOutcome(xml.Remove(position, 1), parse);
                }
            }
        }

        /// <summary>
        /// Замена одного символа на разметочный. Алфавит подобран не случайно:
        /// это ровно те символы, вокруг которых устроен весь разбор, - открывающая
        /// и закрывающая скобка, обе кавычки, '=', ':', '/', '&amp;' и ']'.
        /// Такая мутация уводит разбор в ветки, до которых обрезание документа
        /// не добирается.
        /// </summary>
        [Fact]
        public void SingleCharacterReplacement_NeverThrowsBoundsError_Test()
        {
            const string alphabet = "<>\"'=:/&]";

            foreach (var (xml, parse) in Subjects())
            {
                for (var position = 0; position < xml.Length; position++)
                {
                    foreach (var replacement in alphabet)
                    {
                        AssertSaneOutcome(
                            xml.Substring(0, position) + replacement + xml.Substring(position + 1),
                            parse
                            );
                    }
                }
            }
        }

        /// <summary>
        /// Головы, каждая из которых ломает разбор атрибутов в своём месте:
        /// имя без '=', '=' без значения, значение без закрывающей кавычки,
        /// префикс без имени. Все они добираются до
        /// <c>XmlScan.ParseAttribute</c>, где раньше не было ни одной проверки
        /// границ - все четыре индекса брались как есть.
        /// </summary>
        [Theory]
        [InlineData("<XmlObject2 a>< /XmlObject2>")]
        [InlineData("<XmlObject2 a:b></XmlObject2>")]
        [InlineData("<XmlObject2 a=></XmlObject2>")]
        [InlineData("<XmlObject2 a=\"1></XmlObject2>")]
        [InlineData("<XmlObject2 a='1></XmlObject2>")]
        [InlineData("<XmlObject2 xmlns:p3></XmlObject2>")]
        [InlineData("<XmlObject2 xmlns:p3=></XmlObject2>")]
        [InlineData("<XmlObject2 :></XmlObject2>")]
        [InlineData("<XmlObject2 ::=\"1\"></XmlObject2>")]
        [InlineData("<XmlObject2 a=\"1\" b></XmlObject2>")]
        [InlineData("<XmlObject2 a=\"1\" b:></XmlObject2>")]
        [InlineData("<XmlObject2 =\"1\"></XmlObject2>")]
        [InlineData("<XmlObject2 a=\"\"></XmlObject2>")]
        [InlineData("<XmlObject2 a=\"1\"/>")]
        [InlineData("<XmlObject2\t\r\na=\"1\"></XmlObject2>")]
        public void BrokenAttributeHead_NeverThrowsBoundsError_Test(string xml)
        {
            AssertSaneOutcome(xml, ParseSimple);
        }

        /// <summary>
        /// Незакрытая разметка пролога и тела: комментарий, CDATA, PI, DOCTYPE.
        /// У каждой из них конец ищется поиском подстроки, и «не нашлось» обязано
        /// быть исключением, а не отрицательным индексом, поехавшим в Slice.
        /// </summary>
        [Theory]
        [InlineData("<!-- unterminated <XmlObject2/>")]
        [InlineData("<XmlObject2><!-- unterminated </XmlObject2>")]
        [InlineData("<![CDATA[ unterminated <XmlObject2/>")]
        [InlineData("<XmlObject2><StringProperty><![CDATA[ unterminated </StringProperty></XmlObject2>")]
        [InlineData("<?pi unterminated <XmlObject2/>")]
        [InlineData("<!DOCTYPE XmlObject2 [<!ELEMENT XmlObject2 (#PCDATA)>")]
        [InlineData("<XmlObject2")]
        [InlineData("<XmlObject2>")]
        [InlineData("</XmlObject2>")]
        [InlineData("<>")]
        [InlineData("</>")]
        [InlineData("<")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no markup at all")]
        [InlineData("<XmlObject2></XmlObject3>")]
        [InlineData("<XmlObject2><IntProperty>1</IntProperty>")]
        [InlineData("<XmlObject2><IntProperty>1</XmlObject2>")]
        public void BrokenMarkup_NeverThrowsBoundsError_Test(string xml)
        {
            AssertSaneOutcome(xml, ParseSimple);
        }

        /// <summary>
        /// Глубоко вложенный документ: разбор тела чужого элемента идёт счётом
        /// баланса тегов, а не рекурсией, и упереться в стек не должен.
        /// </summary>
        [Fact]
        public void DeeplyNestedUnknownElement_DoesNotOverflowStack_Test()
        {
            const int depth = 20000;

            var sb = new System.Text.StringBuilder();
            sb.Append("<XmlObject2><Unknown>");
            for (var i = 0; i < depth; i++)
            {
                sb.Append("<a>");
            }
            for (var i = 0; i < depth; i++)
            {
                sb.Append("</a>");
            }
            sb.Append("</Unknown><IntProperty>123</IntProperty></XmlObject2>");

            var xml = sb.ToString();

            ParseSimple(xml);
        }

        private static IEnumerable<(string Xml, Action<string> Parse)> Subjects()
        {
            yield return (Simple, ParseSimple);
            yield return (WithAttributes, ParseWithAttributes);
            yield return (WithCollection, ParseWithCollection);
        }

        private static void ParseSimple(string xml)
        {
            XmlSerializerDeserializer2.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject2 _
                );
        }

        private static void ParseWithAttributes(string xml)
        {
            XmlSerializerDeserializer4_5.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject5 _
                );
        }

        private static void ParseWithCollection(string xml)
        {
            XmlSerializerDeserializer16_17.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out XmlObject17 _
                );
        }

        /// <summary>
        /// Успех - это тоже нормальный исход: обрезанный документ вправе прочитаться
        /// с потерей членов, строгость разбора здесь не проверяется. Проверяется
        /// только то, что вылетевшее исключение говорит о документе, а не о нашей
        /// арифметике.
        /// </summary>
        private static void AssertSaneOutcome(string xml, Action<string> parse)
        {
            try
            {
                parse(xml);
            }
            catch (InvalidOperationException)
            {
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }
            catch (Exception e)
            {
                Assert.Fail(
                    $"{e.GetType().Name} на входе {Describe(xml)}: {e.Message}"
                    );
            }
        }

        private static string Describe(string xml)
        {
            var shown = xml.Length > 120 ? xml.Substring(0, 120) + "..." : xml;

            return $"[{xml.Length}] \"{shown}\"";
        }
    }
}
