#nullable disable

using System;
using System.Text;
using XmlSerDe.Common;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Кодировщик base64 сам по себе. Отдельно от сериализации он нужен из-за
    /// <see cref="XmlBase64.EncodedLength"/>: это единственное место, где оценщик
    /// длины не повторяет работу писателя, а считает её арифметикой, и разойтись
    /// с писателем он может молча - оценка ведь только занижает буфер.
    /// </summary>
    public class XmlBase64Fixture
    {
        [Fact]
        public void EncodedLength_MatchesEncode_Test()
        {
            for (var length = 0; length <= 16; length++)
            {
                var value = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = (byte)(i * 17);
                }

                Assert.Equal(XmlBase64.Encode(value).Length, XmlBase64.EncodedLength(value));

                var sb = new StringBuilder();
                XmlBase64.Append(sb, value);
                Assert.Equal(XmlBase64.Encode(value), sb.ToString());

                var expectedUtf8 = Encoding.UTF8.GetBytes(XmlBase64.Encode(value));
                var utf8 = new byte[XmlBase64.EncodedLength(value)];
                var written = XmlBase64.EncodeToUtf8(value, utf8);
                Assert.Equal(expectedUtf8.Length, written);
                Assert.Equal(expectedUtf8, utf8);

                var offset = 3;
                var chars = new char[offset + XmlBase64.EncodedLength(value) + 2];
                var charWritten = XmlBase64.EncodeToChars(value, chars, offset);
                Assert.Equal(XmlBase64.EncodedLength(value), charWritten);
                Assert.Equal(XmlBase64.Encode(value), new string(chars, offset, charWritten));
            }
        }

        [Fact]
        public void EncodedLength_OfNull_IsZero_Test()
        {
            Assert.Equal(0, XmlBase64.EncodedLength(null));
        }

        [Fact]
        public void RoundTrip_Test()
        {
            for (var length = 0; length <= 16; length++)
            {
                var value = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = (byte)(255 - i * 13);
                }

                Assert.Equal(value, XmlBase64.Decode(XmlBase64.Encode(value).AsSpan()));
            }
        }

        /// <summary>
        /// То же, что делает и читатель BCL: пробельные символы внутри лексемы
        /// игнорируются, а испорченная лексема - это ошибка, а не пустой массив.
        /// </summary>
        [Fact]
        public void Decode_IgnoresWhitespace_Test()
        {
            var expected = new byte[] { 1, 2, 250, };

            Assert.Equal(expected, XmlBase64.Decode("AQL6".AsSpan()));
            Assert.Equal(expected, XmlBase64.Decode("  AQL6  ".AsSpan()));
            Assert.Equal(expected, XmlBase64.Decode("AQ\r\n L6".AsSpan()));
            Assert.Empty(XmlBase64.Decode("   ".AsSpan()));
            Assert.Empty(XmlBase64.Decode(ReadOnlySpan<char>.Empty));
            Assert.Same(Array.Empty<byte>(), XmlBase64.Decode(ReadOnlySpan<char>.Empty));
            Assert.Same(Array.Empty<byte>(), XmlBase64.Decode("   ".AsSpan()));
        }

        [Fact]
        public void Decode_OfGarbage_Throws_Test()
        {
            Assert.Throws<FormatException>(() => XmlBase64.Decode("A".AsSpan()));
            Assert.Throws<FormatException>(() => XmlBase64.Decode("AQL6=".AsSpan()));
        }
    }
}
