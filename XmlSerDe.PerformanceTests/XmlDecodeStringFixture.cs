using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.PerformanceTests;

/*

|   Method |      Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|--------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Standard | 223.72 ns | 4.493 ns | 8.329 ns | 12.99 |    0.61 | 0.0553 |     232 B |        2.23 |
| Baseline |  17.19 ns | 0.262 ns | 0.322 ns |  1.00 |    0.00 | 0.0249 |     104 B |        1.00 |

*/


[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public class XmlDecodeStringFixture
{
    public static readonly string RawString = "my" + new string(' ', 1) + "string !@#$%^&*()_+|-=\\';[]{},./<>?";
    //https://coderstoolbox.net/string/#!encoding=xml&action=encode&charset=us_ascii
    public static readonly string XmlEncodedString = "my" + new string(' ', 1) + "string !@#$%^&amp;*()_+|-=\\&#39;;[]{},./&lt;&gt;?";

    public XmlDecodeStringFixture()
    {
    }

    [Benchmark]
    public void ViaStringBuilder()
    {
        var sb = new StringBuilder();

        for (var i = 0; i < 100; i++)
        {
            var s = "q123123dfghrdjtyk";
            sb.Append(s);
        }

        var rs = sb.ToString();
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(rs);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        System.Text.Encoding.UTF8.GetBytes(rs, buffer.AsSpan(0, byteCount));
        //fake write to some stream
        ArrayPool<byte>.Shared.Return(buffer);
    }


    [Benchmark]
    public void ViaFakeStringBuilder()
    {
        var sb = new FakeStringBuilder();

        for (var i = 0; i < 100; i++)
        {
            var s = "q123123dfghrdjtyk";
            sb.Append(s);
        }
    }

    public class FakeStringBuilder
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Append(string s)
        {
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(s);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            System.Text.Encoding.UTF8.GetBytes(s, buffer.AsSpan(0, byteCount));
            //fake write to some stream
            ArrayPool<byte>.Shared.Return(buffer);
        }

    }


    //[Benchmark]
    //public string Standard()
    //{
    //    var xmlEncodedString = RoscharToString(XmlEncodedString.AsSpan());
    //    string result = global::System.Net.WebUtility.HtmlDecode(xmlEncodedString);
    //    return result;
    //}

    //[Benchmark(Baseline = true)]
    //public string Baseline()
    //{
    //    string result = RoscharToString(RawString.AsSpan()).ToString();
    //    return result;
    //}

    //[MethodImpl(MethodImplOptions.NoInlining)]
    //private string RoscharToString(ReadOnlySpan<char> rawString)
    //{
    //    return rawString.ToString();
    //}


    #region what the target of serialization?

    //return byte[]:
    //|                   Method |      Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
    //|------------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
    //|        SerializeToStream | 120.76 ns | 2.426 ns | 2.270 ns |  1.00 | 0.0956 |     400 B |        1.00 |
    //| SerializeToStringBuilder |  93.34 ns | 0.806 ns | 0.673 ns |  0.77 | 0.0802 |     336 B |        0.84 |

    //return string:
    //|                   Method |      Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
    //|------------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
    //|        SerializeToStream | 146.63 ns | 2.925 ns | 2.873 ns |  1.00 | 0.0994 |     416 B |        1.00 |
    //| SerializeToStringBuilder |  65.15 ns | 1.129 ns | 0.943 ns |  0.45 | 0.0669 |     280 B |        0.67 |


    //[Benchmark(Baseline = true)]
    //public string SerializeToStream()
    //{
    //    using var ms = new MemoryStream();
    //    SerializeToStream(ms, new XmlObject1());
    //    var xml = Encoding.UTF8.GetString(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    //    return xml;
    //}

    //[Benchmark]
    //public string SerializeToStringBuilder()
    //{
    //    var sb = new StringBuilder();
    //    SerializeToStringBuilder(sb, new XmlObject1());
    //    return sb.ToString();
    //}

    //private void SerializeToStream(global::System.IO.Stream stream, XmlObject1 obj)
    //{
    //    if (obj is null)
    //    {
    //        return;
    //    }

    //    global::XmlSerDe.Generator.Producer.BuiltinCodeParser.WriteStringToStream(stream, "<XmlObject1>");
    //    global::XmlSerDe.Generator.Producer.BuiltinCodeParser.WriteStringToStream(stream, "</XmlObject1>");
    //}

    //private void SerializeToStringBuilder(StringBuilder sb, XmlObject1 obj)
    //{
    //    if (obj is null)
    //    {
    //        return;
    //    }

    //    WriteStringToStream(sb, "<XmlObject1>");
    //    WriteStringToStream(sb, "</XmlObject1>");
    //}

    //public static void WriteStringToStream(StringBuilder sb, string inputString)
    //{
    //    sb.Append(inputString);
    //}


    public class XmlObject1
    {
    }

    #endregion


}