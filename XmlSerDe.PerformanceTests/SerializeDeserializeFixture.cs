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
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.PerformanceTests;

/*

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1702/22H2/2022Update/SunValley2)
13th Gen Intel Core i7-13700H, 1 CPU, 20 logical and 14 physical cores
.NET SDK=7.0.300-preview.23179.2
  [Host]   : .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2
  .NET 6.0 : .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2

Job=.NET 6.0  Runtime=.NET 6.0

|                         Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|        'Serialize: System.Xml' | 4.237 us | 0.0382 us | 0.0357 us | 1.1139 | 0.0305 |   14064 B |
|          'Serialize: XmlSerDe' | 1.414 us | 0.0044 us | 0.0041 us | 0.5512 | 0.0076 |    6920 B |
|    'Serialize: XmlSerDe (est)' | 1.372 us | 0.0134 us | 0.0119 us | 0.3910 | 0.0019 |    4928 B |
| 'Serialize: XmlSerDe (stream)' | 1.426 us | 0.0048 us | 0.0045 us | 0.0458 |      - |     592 B |
|      'Deserialize: System.Xml' | 7.757 us | 0.0297 us | 0.0263 us | 1.2970 | 0.0458 |   16392 B |
|        'Deserialize: XmlSerDe' | 7.789 us | 0.0340 us | 0.0318 us | 0.0610 |      - |     824 B |


BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1702/22H2/2022Update/SunValley2)
13th Gen Intel Core i7-13700H, 1 CPU, 20 logical and 14 physical cores
.NET SDK=7.0.300-preview.23179.2
  [Host]   : .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.4 (7.0.423.11508), X64 RyuJIT AVX2

Job=.NET 7.0  Runtime=.NET 7.0

|                         Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|        'Serialize: System.Xml' | 3.676 us | 0.0351 us | 0.0293 us | 1.0300 | 0.0343 |   12960 B |
|          'Serialize: XmlSerDe' | 1.278 us | 0.0185 us | 0.0173 us | 0.5512 |      - |    6920 B |
|    'Serialize: XmlSerDe (est)' | 1.207 us | 0.0095 us | 0.0084 us | 0.3910 | 0.0019 |    4928 B |
| 'Serialize: XmlSerDe (stream)' | 1.289 us | 0.0026 us | 0.0022 us | 0.0458 |      - |     592 B |
|      'Deserialize: System.Xml' | 7.265 us | 0.0404 us | 0.0359 us | 1.2741 | 0.0610 |   16072 B |
|        'Deserialize: XmlSerDe' | 6.417 us | 0.0210 us | 0.0186 us | 0.0610 |      - |     824 B |


*/

[SimpleJob(RuntimeMoniker.Net70)]
[MemoryDiagnoser]
public class SerializeDeserializeFixture : ComplexFixture
{
    //#region serialize

    //[Benchmark(Description = "Serialize: System.Xml")]
    //public string Serialize_SystemXml_Test()
    //{
    //    return Serialize_SystemXml(DefaultObject);
    //}

    //[Benchmark(Description = "Serialize: XmlSerDe")]
    //public string Serialize_XmlSerDe_Test()
    //{
    //    return Serialize_XmlSerDe(DefaultObject);
    //}

    //[Benchmark(Description = "Serialize: XmlSerDe (est)")]
    //public string Serialize_XmlSerDe_Estimated_Test()
    //{
    //    var dlee = new DefaultLengthEstimatorExhauster();
    //    XmlSerializerDeserializer.Serialize(dlee, DefaultObject, false);
    //    var estimateXmlLength = dlee.EstimatedTotalLength;

    //    var dsbe = new DefaultStringBuilderExhauster(
    //        new StringBuilder(estimateXmlLength)
    //        );
    //    XmlSerializerDeserializer.Serialize(dsbe, DefaultObject, false);
    //    var xml = dsbe.ToString();
    //    return xml;
    //}

    //[Benchmark(Description = "Serialize: XmlSerDe (stream)")]
    //public void Serialize_XmlSerDe_ToStream_Test()
    //{
    //    var be = new Utf8BinaryExhausterEmpty(
    //        );
    //    XmlSerializerDeserializer.Serialize(be, DefaultObject, false);
    //}

    //#endregion

    #region deserialize

    [Benchmark(Description = "Deserialize: System.Xml")]
    public InfoContainer Deserialize_SystemXml_Test()
    {
        return Deserialize_SystemXml(AuxXml);
    }

    [Benchmark(Description = "Deserialize: XmlSerDe")]
    public InfoContainer Deserialize_XmlSerDe_Test()
    {
        var auxspan = AuxXml.AsSpan();
        return Deserialize_XmlSerDe(auxspan);
    }

    #endregion

}
