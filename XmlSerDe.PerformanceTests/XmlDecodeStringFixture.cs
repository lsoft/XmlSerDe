using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public string Standard()
    {
        var xmlEncodedString = RoscharToString(XmlEncodedString.AsSpan());
        string result = global::System.Net.WebUtility.HtmlDecode(xmlEncodedString);
        return result;
    }

    [Benchmark(Baseline = true)]
    public string Baseline()
    {
        string result = RoscharToString(RawString.AsSpan()).ToString();
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string RoscharToString(ReadOnlySpan<char> rawString)
    {
        return rawString.ToString();
    }


}