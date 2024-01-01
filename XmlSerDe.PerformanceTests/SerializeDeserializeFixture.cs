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



BenchmarkDotNet v0.13.8, Windows 11 (10.0.22621.2283/22H2/2022Update/SunValley2)
13th Gen Intel Core i7-13700H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 8.0.100-preview.7.23376.3
  [Host]   : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2

Job=.NET 7.0  Runtime=.NET 7.0

| Method                         | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
| 'Serialize: System.Xml'        | 3.823 us | 0.0575 us | 0.0537 us | 1.0529 | 0.0305 |   13296 B |
| 'Serialize: XmlSerDe'          | 1.395 us | 0.0143 us | 0.0134 us | 0.5779 |      - |    7264 B |
| 'Serialize: XmlSerDe (est)'    | 1.357 us | 0.0064 us | 0.0060 us | 0.4368 | 0.0038 |    5480 B |
| 'Serialize: XmlSerDe (stream)' | 1.453 us | 0.0169 us | 0.0158 us | 0.0572 |      - |     720 B |
| 'Deserialize: System.Xml'      | 7.724 us | 0.0549 us | 0.0514 us | 1.3428 | 0.0610 |   16888 B |
| 'Deserialize: XmlSerDe'        | 5.919 us | 0.0358 us | 0.0335 us | 0.0916 |      - |    1216 B |



BenchmarkDotNet v0.13.8, Windows 11 (10.0.22621.2283/22H2/2022Update/SunValley2)
13th Gen Intel Core i7-13700H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 8.0.100-preview.7.23376.3
  [Host]   : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.37506), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0

| Method                         | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
| 'Serialize: System.Xml'        | 3.504 us | 0.0089 us | 0.0074 us | 1.0719 | 0.0381 |   13456 B |
| 'Serialize: XmlSerDe'          | 1.114 us | 0.0050 us | 0.0041 us | 0.5779 |      - |    7264 B |
| 'Serialize: XmlSerDe (est)'    | 1.086 us | 0.0103 us | 0.0091 us | 0.4368 | 0.0038 |    5480 B |
| 'Serialize: XmlSerDe (stream)' | 1.185 us | 0.0081 us | 0.0071 us | 0.0572 |      - |     720 B |
| 'Deserialize: System.Xml'      | 7.260 us | 0.0721 us | 0.0639 us | 1.3428 | 0.0610 |   16879 B |
| 'Deserialize: XmlSerDe'        | 6.137 us | 0.0649 us | 0.0607 us | 0.0916 |      - |    1216 B |

| Method                    | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
| 'Deserialize: System.Xml' | 7.588 us | 0.1345 us | 0.1123 us | 1.3428 | 0.0610 |  16.48 KB |
| 'Deserialize: XmlSerDe'   | 6.069 us | 0.0150 us | 0.0140 us | 0.0916 |      - |   1.19 KB |

*/

//[SimpleJob(RuntimeMoniker.Net70)]
[SimpleJob(RuntimeMoniker.Net80)]
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
