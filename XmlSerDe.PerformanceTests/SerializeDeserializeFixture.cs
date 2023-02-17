using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using XmlSerDe.PerformanceTests.Subject;
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


*/

[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public class SerializeDeserializeFixture
{
    private readonly XmlSerializer _xmlSerializerContainer = new XmlSerializer(
        typeof(InfoContainer),
        new[]
        {
            typeof(Derived3Info),
            typeof(Derived1Info),
            typeof(Derived2Info),
            typeof(SerializeKeyValue),
            typeof(PerformanceTime)
        }
        );

    public static readonly InfoContainer DefaultObject = new InfoContainer
    {
        InfoCollection = new List<BaseInfo>
        {
            new Derived3Info
            {
                Email = "example@example.com"
            },
            new Derived1Info
            {
                BasePersonificationInfo = RawString
            },
            new Derived2Info
            {
                HotKeyUsed = false,
                StepsCounter = 1,
                EventsTime = new List<SerializeKeyValue>
                {
                    new SerializeKeyValue
                    {
                        Key = KeyValueKindEnum.Three,
                        Value = new PerformanceTime { SecondsSpan = 3, StartTime = DateTime.Parse("2022-09-28T14:51:39.2438815+03:00") }
                    },
                    new SerializeKeyValue
                    {
                        Key = KeyValueKindEnum.One,
                        Value = new PerformanceTime { SecondsSpan = 0, StartTime = DateTime.Parse("2022-09-28T14:28:00.5009069+03:00") }
                    },
                    new SerializeKeyValue
                    {
                        Key = KeyValueKindEnum.Two,
                        Value = new PerformanceTime { SecondsSpan = 1, StartTime = DateTime.Parse("2022-09-28T14:28:02.3089553+03:00") }
                    },
                }
            }
        }
    };

    public const string RawString = "my string !@#$%^&*()_+|-=\\';[]{},./<>?";
    //https://coderstoolbox.net/string/#!encoding=xml&action=encode&charset=us_ascii
    public const string XmlEncodedString = "my string !@#$%^&amp;*()_+|-=\\&#39;;[]{},./&lt;&gt;?";

    public const string AuxXml = @"
<InfoContainer>
    <InfoCollection>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived1Info"">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived2Info"">
            <HotKeyUsed>false</HotKeyUsed>
            <StepsCounter>1</StepsCounter>
            <EventsTime>
                <SerializeKeyValue>
                    <Key>Three</Key>
                    <Value>
                        <StartTime>2022-09-28T14:51:39.2438815+03:00</StartTime>
                        <SecondsSpan>3</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>One</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:00.5009069+03:00</StartTime>
                        <SecondsSpan>0</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>Two</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:02.3089553+03:00</StartTime>
                        <SecondsSpan>1</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
            </EventsTime>
        </BaseInfo>
    </InfoCollection>
</InfoContainer>
";

    public const string PartXml = @"          <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
<Email>example@example.com</Email>
</BaseInfo>";

    public const string InternalsXml = @"
    <InfoCollection>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived1Info"">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived2Info"">
            <HotKeyUsed>false</HotKeyUsed>
            <StepsCounter>1</StepsCounter>
            <EventsTime>
                <SerializeKeyValue>
                    <Key>Three</Key>
                    <Value>
                        <StartTime>2022-09-28T14:51:39.2438815+03:00</StartTime>
                        <SecondsSpan>3</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>One</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:00.5009069+03:00</StartTime>
                        <SecondsSpan>0</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>Two</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:02.3089553+03:00</StartTime>
                        <SecondsSpan>1</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
            </EventsTime>
        </BaseInfo>
    </InfoCollection>
";

    public SerializeDeserializeFixture()
    {
        //heat up
        _ = Deserialize_SystemXml_Test();
        _ = Deserialize_XmlSerDe_Test();

        Deserialize_CheckForEquality();
        Serialize_CheckForEquality();
    }

    private void Serialize_CheckForEquality()
    {
        //serialize
        var ser_system = Serialize_SystemXml_Test();
        var ser_xmlserde = Serialize_XmlSerDe_Test();

        //deserialize with different deserializer
        var first = Deserialize_SystemXml(ser_xmlserde);
        var second = Deserialize_XmlSerDe(XmlSerDe.Generator.Producer.BuiltinCodeParser.CutXmlHead(ser_system.AsSpan()));

        CheckForEquality(first, second);
    }

    private void Deserialize_CheckForEquality()
    {
        var deser_systemxml = Deserialize_SystemXml(AuxXml);
        var deser_xmlserde = Deserialize_XmlSerDe(AuxXml.AsSpan());

        CheckForEquality(deser_systemxml, deser_xmlserde);
    }

    private void CheckForEquality(InfoContainer first, InfoContainer second)
    {
        if (first.InfoCollection.Count != second.InfoCollection.Count)
        {
            throw new Exception("InfoCollection.Count");
        }

        {
            var firstau = (Derived3Info)first.InfoCollection[0];
            var secondau = (Derived3Info)second.InfoCollection[0];
            if (firstau.InfoType != secondau.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (firstau.PhoneNumber != secondau.PhoneNumber)
            {
                throw new Exception("PhoneNumber");
            }
            if (firstau.Email != secondau.Email)
            {
                throw new Exception("Email");
            }
        }
        {
            var firstic = (Derived1Info)first.InfoCollection[1];
            var secondic = (Derived1Info)second.InfoCollection[1];
            if (firstic.InfoType != secondic.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (firstic.BasePersonificationInfo != secondic.BasePersonificationInfo)
            {
                throw new Exception("BasePersonificationInfo");
            }
        }
        {
            var firstic = (Derived2Info)first.InfoCollection[2];
            var secondic = (Derived2Info)second.InfoCollection[2];
            if (firstic.InfoType != secondic.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (firstic.StepsCounter != secondic.StepsCounter)
            {
                throw new Exception("StepsCounter");
            }
            if (firstic.HotKeyUsed != secondic.HotKeyUsed)
            {
                throw new Exception("HotKeyUsed");
            }
            if (firstic.EventsTime.Count != secondic.EventsTime.Count)
            {
                throw new Exception("EventsTime.Count");
            }
            for (var eti = 0; eti < firstic.EventsTime.Count; eti++)
            {
                var firstet = firstic.EventsTime[eti];
                var secondet = secondic.EventsTime[eti];
                if (firstet.Key != secondet.Key)
                {
                    throw new Exception("Key");
                }
                if (firstet.Value.StartTime != secondet.Value.StartTime)
                {
                    throw new Exception("Value.StartTime");
                }
                if (firstet.Value.SecondsSpan != secondet.Value.SecondsSpan)
                {
                    throw new Exception("Value.SecondsSpan");
                }
            }
        }
    }



    [Benchmark(Description = "Serialize: System.Xml")]
    public string Serialize_SystemXml_Test()
    {
        using var ms = new MemoryStream();
        _xmlSerializerContainer.Serialize(ms, DefaultObject);
        var xml = Encoding.UTF8.GetString(ms.ToArray());
        return xml;
    }

    [Benchmark(Description = "Serialize: XmlSerDe")]
    public string Serialize_XmlSerDe_Test()
    {
        using var ms = new MemoryStream();
        XmlSerializerDeserializer.Serialize(ms, DefaultObject, false);
        var xml = Encoding.UTF8.GetString(ms.ToArray());
        return xml;
    }




    [Benchmark(Description = "Deserialize: System.Xml")]
    public InfoContainer Deserialize_SystemXml_Test()
    {
        return Deserialize_SystemXml(AuxXml);
    }

    [Benchmark(Description = "Deserialize: XmlSerDe")]
    public InfoContainer Deserialize_XmlSerDe_Test()
    {
        InfoContainer r = Deserialize_XmlSerDe(AuxXml.AsSpan());
        return r;
    }








    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private InfoContainer Deserialize_SystemXml(string xml)
    {
        using (var reader = new StringReader(xml))
        {
            var r = (InfoContainer)_xmlSerializerContainer.Deserialize(reader);
            return r;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static InfoContainer Deserialize_XmlSerDe(ReadOnlySpan<char> xml)
    {
        XmlSerializerDeserializer.Deserialize(xml, out InfoContainer r);
        return r;
    }
}
