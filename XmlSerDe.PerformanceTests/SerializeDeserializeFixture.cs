using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Serialization;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.PerformanceTests;

/*
BenchmarkDotNet=v0.13.4, OS=Windows 10 (10.0.19044.1586/21H2/November2021Update)
Intel Core i7-8550U CPU 1.80GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.102
[Host]   : .NET 6.0.13 (6.0.1322.58009), X64 RyuJIT AVX2
.NET 6.0 : .NET 6.0.13 (6.0.1322.58009), X64 RyuJIT AVX2

Job=.NET 6.0  Runtime=.NET 6.0

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 18.81 us | 0.373 us | 0.753 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 21.33 us | 0.424 us | 0.797 us |  1.13 |    0.06 | 0.4272 |   1.76 KB |        0.10 |

| Method |     Mean |    Error |   StdDev |   Median | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 18.76 us | 0.402 us | 1.154 us | 18.23 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 27.96 us | 0.548 us | 1.056 us | 27.69 us |  1.51 |    0.11 | 0.4272 |   1.76 KB |        0.10 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 17.52 us | 0.341 us | 0.302 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 25.96 us | 0.501 us | 0.780 us |  1.48 |    0.07 | 0.4272 |   1.76 KB |        0.10 | 

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 18.06 us | 0.305 us | 0.270 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 23.10 us | 0.447 us | 0.695 us |  1.26 |    0.04 | 0.4272 |   1.76 KB |        0.10 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 18.60 us | 0.281 us | 0.385 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 15.23 us | 0.270 us | 0.472 us |  0.82 |    0.03 | 0.4272 |   1.76 KB |        0.10 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 18.74 us | 0.372 us | 0.471 us |  1.00 |    0.00 | 4.0894 |  16.76 KB |        1.00 |
|    New | 13.99 us | 0.201 us | 0.407 us |  0.76 |    0.03 | 0.4272 |   1.76 KB |        0.10 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 15.54 us | 0.284 us | 0.252 us |  1.00 |    0.00 | 3.9063 |   16392 B |        1.00 |
|    New | 13.01 us | 0.258 us | 0.242 us |  0.84 |    0.02 | 0.1984 |     848 B |        0.05 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 15.86 us | 0.313 us | 0.487 us |  1.00 |    0.00 | 3.9063 |   16392 B |        1.00 |
|    New | 13.04 us | 0.148 us | 0.115 us |  0.84 |    0.03 | 0.1984 |     848 B |        0.05 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 16.08 us | 0.312 us | 0.486 us |  1.00 |    0.00 | 3.9063 |   16392 B |        1.00 |
|    New | 12.22 us | 0.232 us | 0.276 us |  0.76 |    0.03 | 0.1831 |     848 B |        0.05 |

| Method |     Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
|    Old | 15.80 us | 0.248 us | 0.220 us |  1.00 | 3.9063 |   16392 B |        1.00 |
|    New | 11.31 us | 0.174 us | 0.145 us |  0.72 | 0.1984 |     848 B |        0.05 |

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|    Old | 16.05 us | 0.299 us | 0.307 us |  1.00 |    0.00 | 3.9063 |   16392 B |        1.00 |
|    New | 11.34 us | 0.212 us | 0.165 us |  0.71 |    0.02 | 0.1678 |     736 B |        0.04 |

|     Method |     Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
|----------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| System.Xml | 15.92 us | 0.234 us | 0.219 us |  1.00 | 3.9063 |   16392 B |        1.00 |
|   XmlSerDe | 11.80 us | 0.112 us | 0.137 us |  0.74 | 0.1678 |     736 B |        0.04 |

|                    Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|-------------------------- |----------:|----------:|----------:|-------:|----------:|
|   'Serialize: System.Xml' |  9.172 us | 0.1578 us | 0.3040 us | 3.6774 |   15448 B |
|     'Serialize: XmlSerDe' |  5.152 us | 0.0983 us | 0.0965 us | 1.8234 |    7641 B |
| 'Deserialize: System.Xml' | 15.678 us | 0.2961 us | 0.2625 us | 3.9063 |   16392 B |
|   'Deserialize: XmlSerDe' | 11.906 us | 0.1170 us | 0.0977 us | 0.1678 |     736 B |

|                    Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|-------------------------- |----------:|----------:|----------:|-------:|----------:|
|   'Serialize: System.Xml' |  8.320 us | 0.1332 us | 0.1246 us | 3.3569 |   14064 B |
|     'Serialize: XmlSerDe' |  5.041 us | 0.1006 us | 0.1235 us | 1.5640 |    6569 B |
| 'Deserialize: System.Xml' | 15.838 us | 0.2321 us | 0.3099 us | 3.9063 |   16392 B |
|   'Deserialize: XmlSerDe' | 12.033 us | 0.2402 us | 0.2571 us | 0.1678 |     736 B |


estimation of result string length:
|                  Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|------------------------ |----------:|----------:|----------:|-------:|----------:|
| 'Serialize: System.Xml' |  8.429 us | 0.0942 us | 0.0881 us | 3.3569 |   14064 B |
|   'Serialize: XmlSerDe' |  4.735 us | 0.0574 us | 0.0509 us | 1.1292 |    4753 B |


|                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|---------------------------- |----------:|----------:|----------:|-------:|----------:|
|     'Serialize: System.Xml' |  8.582 us | 0.1637 us | 0.1608 us | 3.3569 |   14064 B |
|       'Serialize: XmlSerDe' |  4.984 us | 0.0823 us | 0.0687 us | 1.5640 |    6569 B |
| 'Serialize: XmlSerDe (est)' |  4.722 us | 0.0865 us | 0.0767 us | 1.1292 |    4753 B |
|   'Deserialize: System.Xml' | 15.273 us | 0.1385 us | 0.1081 us | 3.9063 |   16392 B |
|     'Deserialize: XmlSerDe' | 11.598 us | 0.1934 us | 0.2302 us | 0.1678 |     736 B |


------------------------------  BASELINE ON MY LAPTOP  ---------------------------------
|                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|---------------------------- |----------:|----------:|----------:|-------:|----------:|
|     'Serialize: System.Xml' |  7.604 us | 0.0608 us | 0.0508 us | 6.7062 |   14064 B |
|       'Serialize: XmlSerDe' |  4.310 us | 0.0240 us | 0.0225 us | 3.1357 |    6569 B |
| 'Serialize: XmlSerDe (est)' |  4.340 us | 0.0202 us | 0.0189 us | 2.2659 |    4753 B |
|   'Deserialize: System.Xml' | 13.892 us | 0.0953 us | 0.0845 us | 7.8125 |   16392 B |
|     'Deserialize: XmlSerDe' | 10.754 us | 0.0444 us | 0.0393 us | 0.3510 |     736 B |

----------------------------  Serialize to StringBuilder  ------------------------------
|                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|---------------------------- |----------:|----------:|----------:|-------:|----------:|
|     'Serialize: System.Xml' |  7.734 us | 0.0516 us | 0.0483 us | 6.6986 |   14064 B |
|       'Serialize: XmlSerDe' |  2.581 us | 0.0190 us | 0.0177 us | 3.2921 |    6889 B |
| 'Serialize: XmlSerDe (est)' |  2.325 us | 0.0092 us | 0.0082 us | 2.3117 |    4841 B | <---
|   'Deserialize: System.Xml' | 13.814 us | 0.0391 us | 0.0327 us | 7.8125 |   16392 B |
|     'Deserialize: XmlSerDe' | 10.664 us | 0.0194 us | 0.0172 us | 0.3510 |     736 B |

*/

[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public class SerializeDeserializeFixture : ComplexFixture
{
    #region serialize

    [Benchmark(Description = "Serialize: System.Xml")]
    public string Serialize_SystemXml_Test()
    {
        return Serialize_SystemXml(DefaultObject);
    }

    [Benchmark(Description = "Serialize: XmlSerDe")]
    public string Serialize_XmlSerDe_Test()
    {
        return Serialize_XmlSerDe(DefaultObject);
    }


    [Benchmark(Description = "Serialize: XmlSerDe (est)")]
    public string Serialize_XmlSerDe_Estimated_Test()
    {
        var estimatedSize = 1100; //TODO: estimate it via XmlSerializerDeserializer.EstimateSerializedSize(ref esimatedCharCount);
        var sb = new StringBuilder(estimatedSize);
        XmlSerializerDeserializer.Serialize(sb, DefaultObject, false);
        var xml = sb.ToString();
        return xml;
    }

    #endregion

    #region deserialize

    [Benchmark(Description = "Deserialize: System.Xml")]
    public InfoContainer Deserialize_SystemXml_Test()
    {
        return Deserialize_SystemXml(AuxXml);
    }

    [Benchmark(Description = "Deserialize: XmlSerDe")]
    public InfoContainer Deserialize_XmlSerDe_Test()
    {
        return Deserialize_XmlSerDe(AuxXml.AsSpan());
    }

    #endregion

}
