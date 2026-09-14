using System;
using System.IO;
using System.Xml.Serialization;
using XmlSerDe;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Формы POCO из <see cref="ReviewShapeHosts"/>: сгенерированный код
    /// компилировался, но читал или писал не то, что System.Xml.Serialization.
    /// Эталонный XML в каждом тесте получен от штатного сериализатора.
    /// </summary>
    public class ReviewShapeFixture
    {
        private const string Xsi = "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";

        #region [XmlArrayItem] на коллекции сложного типа

        [Fact]
        public void ArrayItem_ComplexCollection_RoundTrips()
        {
            var sb = new StringBuilderExhauster();
            ReviewCollectionHost.Serialize(
                sb,
                new ArrayItemHolder { Points = { new ArrayItemPoint { X = 1 } } },
                false
                );
            var xml = sb.ToString();

            Assert.Equal("<ArrayItemHolder><Points><item><X>1</X></item></Points></ArrayItemHolder>", xml);

            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out ArrayItemHolder result
                );

            var point = Assert.Single(result.Points);
            Assert.Equal(1, point.X);
        }

        [Fact]
        public void ArrayItem_ComplexCollection_ReadsBclOutput()
        {
            var bcl = new XmlSerializer(typeof(ArrayItemHolder));
            var sw = new StringWriter();
            bcl.Serialize(sw, new ArrayItemHolder { Points = { new ArrayItemPoint { X = 2 }, new ArrayItemPoint { X = 3 } } });

            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                ReviewCollectionHost.CutXmlHead(sw.ToString().AsSpan()),
                out ArrayItemHolder result
                );

            Assert.Equal(2, result.Points.Count);
            Assert.Equal(2, result.Points[0].X);
            Assert.Equal(3, result.Points[1].X);
        }

        [Fact]
        public void ArrayItem_ForeignItemName_StillThrows()
        {
            Assert.ThrowsAny<Exception>(
                () => ReviewCollectionHost.Deserialize(
                    DefaultInjector.Instance,
                    "<ArrayItemHolder><Points><ArrayItemPoint><X>1</X></ArrayItemPoint></Points></ArrayItemHolder>".AsSpan(),
                    out ArrayItemHolder _
                    )
                );
        }

        #endregion

        #region пустые элементы коллекции

        /// <summary>
        /// System.Xml.Serialization пишет пустую строку в списке как
        /// <c>&lt;string /&gt;</c>, а объект без записанных членов - как
        /// <c>&lt;Point /&gt;</c>. Это элементы, а не их отсутствие.
        /// </summary>
        [Theory]
        [InlineData("<EmptyItemsStrings><Items><string>a</string><string /></Items></EmptyItemsStrings>")]
        [InlineData("<EmptyItemsStrings><Items><string>a</string><string></string></Items></EmptyItemsStrings>")]
        [InlineData("<EmptyItemsStrings><Items><string>a</string><string\n/></Items></EmptyItemsStrings>")]
        public void EmptyStringItem_IsAnEmptyString(string xml)
        {
            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out EmptyItemsStrings result
                );

            Assert.Equal(new[] { "a", "" }, result.Items);
        }

        [Fact]
        public void NilStringItem_IsNull()
        {
            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                ("<EmptyItemsStrings " + Xsi + "><Items><string>a</string><string xsi:nil=\"true\" /><string>b</string></Items></EmptyItemsStrings>").AsSpan(),
                out EmptyItemsStrings result
                );

            Assert.Equal(new[] { "a", null, "b" }, result.Items);
        }

        [Fact]
        public void EmptyObjectItem_IsADefaultObject()
        {
            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                "<EmptyItemsObjects><Items><ArrayItemPoint /><ArrayItemPoint><X>1</X></ArrayItemPoint></Items></EmptyItemsObjects>".AsSpan(),
                out EmptyItemsObjects result
                );

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(0, result.Items[0].X);
            Assert.Equal(1, result.Items[1].X);
        }

        [Fact]
        public void EmptyItems_MatchBclReading()
        {
            const string xml = "<EmptyItemsStrings><Items><string>a</string><string /><string>b</string></Items></EmptyItemsStrings>";

            var expected = (EmptyItemsStrings)new XmlSerializer(typeof(EmptyItemsStrings)).Deserialize(new StringReader(xml))!;

            ReviewCollectionHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out EmptyItemsStrings result
                );

            Assert.Equal(expected.Items, result.Items);
        }

        #endregion

        #region многоуровневый XmlInclude

        [Fact]
        public void TransitiveInclude_WritesMostDerivedXsiType()
        {
            var sb = new StringBuilderExhauster();
            ReviewIncludeHost.Serialize(
                sb,
                new IncHolder { Item = new IncLeaf { A = 1, B = 2, C = 3 } },
                false
                );

            Assert.Equal(
                "<IncHolder><Item " + Xsi + " xsi:type=\"IncLeaf\"><A>1</A><B>2</B><C>3</C></Item></IncHolder>",
                sb.ToString()
                );
        }

        [Theory]
        [InlineData("IncLeaf", typeof(IncLeaf), 3)]
        [InlineData("IncMid", typeof(IncMid), 0)]
        public void TransitiveInclude_ReadsLeafXsiType(string xsiType, Type expectedType, int expectedC)
        {
            ReviewIncludeHost.Deserialize(
                DefaultInjector.Instance,
                ("<IncHolder><Item " + Xsi + " xsi:type=\"" + xsiType + "\"><A>1</A><B>2</B><C>3</C></Item></IncHolder>").AsSpan(),
                out IncHolder result
                );

            Assert.IsType(expectedType, result.Item);
            Assert.Equal(1, result.Item!.A);
            Assert.Equal(2, ((IncMid)result.Item).B);
            Assert.Equal(expectedC, result.Item is IncLeaf leaf ? leaf.C : 0);
        }

        [Fact]
        public void TransitiveInclude_MatchesBclDocument()
        {
            var bcl = new XmlSerializer(typeof(IncHolder));
            var sw = new StringWriter();
            bcl.Serialize(sw, new IncHolder { Item = new IncLeaf { A = 1, B = 2, C = 3 } });

            ReviewIncludeHost.Deserialize(
                DefaultInjector.Instance,
                ReviewIncludeHost.CutXmlHead(sw.ToString().AsSpan()),
                out IncHolder result
                );

            var leaf = Assert.IsType<IncLeaf>(result.Item);
            Assert.Equal(3, leaf.C);
        }

        /// <summary>
        /// Оба наследника объявлены на базе, родитель раньше ребёнка: проверка
        /// <c>obj is FlatMid</c> сработала бы на листе первой и потеряла его.
        /// </summary>
        [Fact]
        public void FlatIncludes_DispatchMostDerivedFirst()
        {
            var sb = new StringBuilderExhauster();
            ReviewIncludeHost.Serialize(
                sb,
                new FlatHolder { Item = new FlatLeaf { A = 1, B = 2, C = 3 } },
                false
                );

            Assert.Equal(
                "<FlatHolder><Item " + Xsi + " xsi:type=\"FlatLeaf\"><A>1</A><B>2</B><C>3</C></Item></FlatHolder>",
                sb.ToString()
                );
        }

        #endregion
    }
}
