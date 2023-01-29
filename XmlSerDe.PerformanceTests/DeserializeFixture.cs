using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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



*/

[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public class DeserializeFixture
{
    private readonly XmlSerializer _xmlSerializerContainer = new XmlSerializer(
        typeof(InfoContainer),
        new[]
        {
            typeof(Derived3Info),
            typeof(Derived1Info),
            typeof(Derived2Info)
        }
        );

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

    public DeserializeFixture()
    {
        _ = Old();
        _ = New();

        CheckForEquality();
    }

    private void CheckForEquality()
    {
        var oldr = Old();
        var newr = New();

        if (oldr.InfoCollection.Count != newr.InfoCollection.Count)
        {
            throw new Exception("InfoCollection.Count");
        }
        {
            var oldau = (Derived3Info)oldr.InfoCollection[0];
            var newau = (Derived3Info)newr.InfoCollection[0];
            if (oldau.InfoType != newau.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (oldau.PhoneNumber != newau.PhoneNumber)
            {
                throw new Exception("PhoneNumber");
            }
            if (oldau.Email != newau.Email)
            {
                throw new Exception("Email");
            }
        }
        {
            var oldau = (Derived1Info)oldr.InfoCollection[1];
            var newau = (Derived1Info)newr.InfoCollection[1];
            if (oldau.InfoType != newau.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (oldau.BasePersonificationInfo != newau.BasePersonificationInfo)
            {
                throw new Exception("BasePersonificationInfo");
            }
        }
        {
            var oldau = (Derived2Info)oldr.InfoCollection[2];
            var newau = (Derived2Info)newr.InfoCollection[2];
            if (oldau.InfoType != newau.InfoType)
            {
                throw new Exception("InfoType");
            }
            if (oldau.StepsCounter != newau.StepsCounter)
            {
                throw new Exception("StepsCounter");
            }
            if (oldau.HotKeyUsed != newau.HotKeyUsed)
            {
                throw new Exception("HotKeyUsed");
            }
            if (oldau.EventsTime.Count != newau.EventsTime.Count)
            {
                throw new Exception("EventsTime.Count");
            }
            for (var eti = 0; eti < oldau.EventsTime.Count; eti++)
            {
                var oldet = oldau.EventsTime[eti];
                var newet = newau.EventsTime[eti];
                if (oldet.Key != newet.Key)
                {
                    throw new Exception("Key");
                }
                if (oldet.Value.StartTime != newet.Value.StartTime)
                {
                    throw new Exception("Value.StartTime");
                }
                if (oldet.Value.SecondsSpan != newet.Value.SecondsSpan)
                {
                    throw new Exception("Value.SecondsSpan");
                }
            }
        }
    }

    [Benchmark(Baseline = true)]
    public InfoContainer Old()
    {
        using (var reader = new StringReader(AuxXml))
        {
            var r = (InfoContainer)_xmlSerializerContainer.Deserialize(reader);
            return r;
        }

    }

    [Benchmark]
    public InfoContainer New()
    {
        XmlSerializerDeserializer.Deserialize(AuxXml.AsSpan(), out InfoContainer r);
        return r;
    }
}
