using System;
using System.Linq;
using System.Text;
using XmlSerDe;
using XmlSerDe.Internal;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Array members are no longer accumulated in a List&lt;T&gt;: the
    /// intermediate buffers now come from ArrayPool via
    /// <see cref="PooledArrayBuilder{T}"/>, so the only allocation left is the
    /// resulting array itself.
    ///
    /// Every pre-existing array test uses exactly three elements, and the
    /// builder starts at capacity 8 - which means none of them ever reaches
    /// the growth path, and a bug there would have shipped unnoticed. Hence
    /// the sizes here: 8 is the first boundary, and 1000 crosses it seven
    /// times, each crossing copying the accumulated elements into a larger
    /// rented buffer.
    ///
    /// Reference and struct element types matter separately: their buffers are
    /// returned to the pool cleared, so that the pool does not keep
    /// deserialized objects alive after the caller has dropped them.
    /// </summary>
    public class PooledArrayBuilderFixture
    {
        #region growth across pooled buffers

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(100)]
        [InlineData(1000)]
        public void IntArray_OfAnyLength_RoundTrips(int count)
        {
            var expected = Enumerable.Range(-count / 2, count).ToArray();

            var xml = new StringBuilder();
            xml.Append("<XmlObject19><Ints>");
            foreach (var value in expected)
            {
                xml.Append("<int>").Append(value).Append("</int>");
            }
            xml.Append("</Ints></XmlObject19>");

            XmlSerializerDeserializer19.Deserialize(
                DefaultInjector.Instance,
                xml.ToString().AsSpan(),
                out XmlObject19 deserialized
                );

            Assert.Equal(expected, deserialized.Ints);
        }

        [Fact]
        public void ReferenceTypeArray_CrossingSeveralGrowthSteps_RoundTrips()
        {
            //a reference element type takes the other branch of ClearOnReturn:
            //the rented buffer goes back to the pool cleared, so the pool cannot
            //keep deserialized objects alive - and the elements must still
            //survive the copy into the result
            const int Count = 300;

            var xml = new StringBuilder();
            xml.Append("<XmlObject23><Objs>");
            for (var i = 0; i < Count; i++)
            {
                xml.Append("<XmlObject24></XmlObject24>");
            }
            xml.Append("</Objs></XmlObject23>");

            XmlSerializerDeserializer23_24.Deserialize(
                DefaultInjector.Instance,
                xml.ToString().AsSpan(),
                out XmlObject23 deserialized
                );

            Assert.Equal(Count, deserialized.Objs.Length);
            Assert.All(deserialized.Objs, Assert.NotNull);
            //distinct instances, not one instance repeated by a buffer reused
            //across growth steps
            Assert.Equal(Count, deserialized.Objs.Distinct().Count());
        }

        [Fact]
        public void GuidArray_CrossingSeveralGrowthSteps_RoundTrips()
        {
            const int Count = 50;

            var expected = Enumerable
                .Range(0, Count)
                .Select(i => new Guid(i, 0, 0, new byte[8]))
                .ToArray();

            var xml = new StringBuilder();
            xml.Append("<XmlObject21><Guids>");
            foreach (var value in expected)
            {
                xml.Append("<guid>").Append(value.ToString()).Append("</guid>");
            }
            xml.Append("</Guids></XmlObject21>");

            XmlSerializerDeserializer21.Deserialize(
                DefaultInjector.Instance,
                xml.ToString().AsSpan(),
                out XmlObject21 deserialized
                );

            Assert.Equal(expected, deserialized.Guids);
        }

        #endregion

        #region the builder in isolation

        [Fact]
        public void Builder_WithoutAnyItem_YieldsEmptyArray()
        {
            var builder = new PooledArrayBuilder<int>();

            var result = builder.ToArrayAndRelease();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Builder_ReleasesToExactlyTheCountAdded()
        {
            var builder = new PooledArrayBuilder<int>();
            for (var i = 0; i < 33; i++)
            {
                builder.Add(i);
            }

            Assert.Equal(33, builder.Count);

            var result = builder.ToArrayAndRelease();

            //the rented buffer is longer than this - the result must not be
            Assert.Equal(33, result.Length);
            Assert.Equal(Enumerable.Range(0, 33).ToArray(), result);
        }

        [Fact]
        public void Builder_IsReusableAfterRelease()
        {
            var builder = new PooledArrayBuilder<string>();
            builder.Add("a");
            builder.Add("b");

            Assert.Equal(new[] { "a", "b" }, builder.ToArrayAndRelease());

            //release resets the builder, so a second round must not see the first
            Assert.Equal(0, builder.Count);
            builder.Add("c");
            Assert.Equal(new[] { "c" }, builder.ToArrayAndRelease());
        }

        [Fact]
        public void Builder_ToListAndRelease_HasExactCount()
        {
            var builder = new PooledArrayBuilder<int>();
            for (var i = 0; i < 33; i++)
            {
                builder.Add(i);
            }

            var list = builder.ToListAndRelease();

            Assert.Equal(33, list.Count);
            Assert.Equal(33, list.Capacity);
            Assert.Equal(Enumerable.Range(0, 33), list);
            Assert.Equal(0, builder.Count);
        }

        [Fact]
        public void Builder_ToListAndRelease_WithoutItems_IsEmpty()
        {
            var builder = new PooledArrayBuilder<string>();

            var list = builder.ToListAndRelease();

            Assert.Empty(list);
        }

        #endregion
    }
}
