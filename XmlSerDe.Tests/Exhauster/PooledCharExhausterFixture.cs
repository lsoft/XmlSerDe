using System;
using System.IO;
using System.Text;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Deep;
using XmlSerDe.Tests.Huge;
using Xunit;

namespace XmlSerDe.Tests.Exhauster
{
    /// <summary>
    /// Оценённый string-путь пишет в один <c>char[]</c> из пула и копирует
    /// в <c>string</c> только записанный префикс. Документ обязан совпадать
    /// с <see cref="StringBuilderExhauster"/>, в том числе когда оценка
    /// занижена и буфер растёт.
    /// </summary>
    [Collection(ComplexTestsCollection.Name)]
    public class PooledCharExhausterFixture
    {
        [Fact]
        public void Appends_MatchStringBuilder()
        {
            var sb = new StringBuilderExhauster();
            using var pooled = new PooledCharExhauster(1);

            Fill(sb);
            Fill(pooled);

            var expected = sb.ToString();
            Assert.Equal(expected, pooled.ToString());
            Assert.Equal(expected.Length, pooled.Written);
        }

        [Fact]
        public void Regular_MatchesStringBuilder_EvenFromTinyCapacity()
        {
            var expected = ComplexFixture.Serialize_XmlSerDe(ComplexFixture.DefaultObject);

            using var pooled = new PooledCharExhauster(1);
            XmlSerializerDeserializer.Serialize(pooled, ComplexFixture.DefaultObject, false);

            Assert.Equal(expected, pooled.ToString());
            Assert.Equal(expected.Length, pooled.Written);
        }

        [Fact]
        public void Regular_AccEst_CopiesOnlyWrittenPrefix()
        {
            var expected = ComplexFixture.Serialize_XmlSerDe(ComplexFixture.DefaultObject);

            var estimator = new LengthEstimatorExhauster();
            XmlSerializerDeserializer.Serialize(estimator, ComplexFixture.DefaultObject, false);

            using var pooled = new PooledCharExhauster(estimator.EstimatedTotalLength);
            XmlSerializerDeserializer.Serialize(pooled, ComplexFixture.DefaultObject, false);

            Assert.Equal(expected, pooled.ToString());
            Assert.Equal(expected.Length, pooled.Written);
            Assert.True(estimator.EstimatedTotalLength >= pooled.Written);
        }

        [Fact]
        public void Deep_MatchesStringBuilder()
        {
            var expected = DeepFixture.Serialize_XmlSerDe(DeepFixture.DefaultObject);

            var estimator = new LengthEstimatorExhauster();
            DeepXmlSerializerDeserializer.Serialize(estimator, DeepFixture.DefaultObject, false);

            using var pooled = new PooledCharExhauster(estimator.EstimatedTotalLength);
            DeepXmlSerializerDeserializer.Serialize(pooled, DeepFixture.DefaultObject, false);

            Assert.Equal(expected, pooled.ToString());
            Assert.Equal(expected.Length, pooled.Written);
        }

        [Fact]
        public void HugeSmall_MatchesStringBuilder()
        {
            var expected = HugeFixture.Serialize_XmlSerDe(HugeFixture.SmallObject);

            var estimator = new LengthEstimatorExhauster();
            HugeXmlSerializerDeserializer.Serialize(estimator, HugeFixture.SmallObject, false);

            using var pooled = new PooledCharExhauster(estimator.EstimatedTotalLength);
            HugeXmlSerializerDeserializer.Serialize(pooled, HugeFixture.SmallObject, false);

            Assert.Equal(expected, pooled.ToString());
            Assert.Equal(expected.Length, pooled.Written);
        }

        [Fact]
        public void Clear_ReusesBuffer()
        {
            using var pooled = new PooledCharExhauster(32);
            pooled.Append("first");
            pooled.Clear();
            pooled.Append("second");

            Assert.Equal("second", pooled.ToString());
            Assert.Equal(6, pooled.Written);
        }

        [Fact]
        public void Dispose_ThenToString_Throws()
        {
            var pooled = new PooledCharExhauster(16);
            pooled.Append("x");
            pooled.Dispose();

            Assert.Throws<ObjectDisposedException>(() => pooled.ToString());
            Assert.Throws<ObjectDisposedException>(() => pooled.Written);
            pooled.Dispose();
        }

        [Fact]
        public void NegativeCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PooledCharExhauster(-1));
        }

        [Fact]
        public void WriteTo_MatchesToString()
        {
            using var pooled = new PooledCharExhauster(1);
            Fill(pooled);

            var writer = new StringWriter();
            pooled.WriteTo(writer);

            Assert.Equal(pooled.ToString(), writer.ToString());
        }

        [Fact]
        public void Utf8Stream_MatchesStringBuilderUtf8()
        {
            var sb = new StringBuilderExhauster();
            Fill(sb);

            using var stream = new MemoryStream();
            using (var utf8 = new Utf8StreamExhauster(stream))
            {
                Fill(utf8);
            }

            Assert.Equal(Encoding.UTF8.GetBytes(sb.ToString()), stream.ToArray());
        }

        [Fact]
        public void Utf8Stream_LongerThanCoalesceBuffer_MatchesStringBuilderUtf8()
        {
            var text = new string('a', Utf8StreamExhauster.BufferSize + 100);
            var sb = new StringBuilderExhauster();
            sb.Append(text);

            using var stream = new MemoryStream();
            using (var utf8 = new Utf8StreamExhauster(stream))
            {
                utf8.Append(text);
            }

            Assert.Equal(Encoding.UTF8.GetBytes(sb.ToString()), stream.ToArray());
        }

        [Fact]
        public void Utf8Stream_ManyTinyAppends_MatchStringBuilderUtf8()
        {
            const string Tag = "<x/>";
            var sb = new StringBuilderExhauster();
            for (var i = 0; i < 2000; i++)
            {
                sb.Append(Tag);
            }

            using var stream = new MemoryStream();
            using (var utf8 = new Utf8StreamExhauster(stream))
            {
                for (var i = 0; i < 2000; i++)
                {
                    utf8.Append(Tag);
                }
            }

            Assert.Equal(Encoding.UTF8.GetBytes(sb.ToString()), stream.ToArray());
        }

        private static void Fill(IExhauster exhauster)
        {
            exhauster.Append(true);
            exhauster.Append((bool?)null);
            exhauster.Append(-42);
            exhauster.Append((int?)null);
            exhauster.Append(1234567890123L);
            exhauster.Append(12.5d);
            exhauster.Append(float.PositiveInfinity);
            exhauster.Append(99.99m);
            exhauster.Append('A');
            exhauster.Append(Guid.Parse("12345678-1234-5678-9abc-def012345678"));
            exhauster.Append(new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc));
            exhauster.Append(TimeSpan.FromSeconds(1.5));
            exhauster.Append("raw&text");
            exhauster.AppendEncoded("a'b&c");
            exhauster.AppendAttributeEncoded("a\nb");
            exhauster.AppendBase64(new byte[] { 1, 2, 250 });
            exhauster.AppendBase64(Array.Empty<byte>());
        }
    }
}
