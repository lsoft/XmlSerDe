using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftAntimalwareEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using XmlSerDe.Generator.EmbeddedCode;
using XmlSerDe.PerformanceTests.Subject;

namespace XmlSerDe.PerformanceTests
{
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


*/

    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            var oldr = new Fixture().Old();
            var newr = new Fixture().New();

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
                for(var eti = 0; eti < oldau.EventsTime.Count; eti++)
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

            int g = 0;

            //var qo = new Fixture().OrigScan();
            //var q4 = new Fixture().NewScan4();

            //if (qo.IsEmpty != q4.IsEmpty)
            //{
            //    throw new Exception("IsEmpty");
            //}
            //if (qo.IsBodyless != q4.IsBodyless)
            //{
            //    throw new Exception("bodyless");
            //}
            //if (MemoryExtensions.SequenceCompareTo(qo.FullHead, q4.FullHead) != 0)
            //{
            //    throw new Exception("FullHead");
            //}
            //if (MemoryExtensions.SequenceCompareTo(qo.NodeType, q4.NodeType) != 0)
            //{
            //    throw new Exception("FullHead");
            //}

            //var qq = Fixture.ScanHeadOrig("asdas@asdasd.ru".AsSpan());

            //var orig = Fixture.GetFirst_Orig(Fixture.InternalsXml.AsSpan());
            //var faster = Fixture.GetFirst_New2(Fixture.InternalsXml.AsSpan());
            //if (MemoryExtensions.SequenceCompareTo(orig.FullNode, faster.FullNode) != 0)
            //{
            //    throw new Exception("FullNode");
            //}
            //if (MemoryExtensions.SequenceCompareTo(orig.Head.FullHead, faster.Head.FullHead) != 0)
            //{
            //    throw new Exception("Head.FullHead");
            //}
            //if (MemoryExtensions.SequenceCompareTo(orig.Head.NodeType, faster.Head.NodeType) != 0)
            //{
            //    throw new Exception("Head.NodeType");
            //}
            //if (MemoryExtensions.SequenceCompareTo(orig.Internals, faster.Internals) != 0)
            //{
            //    throw new Exception("Internals");
            //}
            //if (orig.IsEmpty != faster.IsEmpty)
            //{
            //    throw new Exception("IsEmpty");
            //}


            var orig = Fixture.ParseFirstFoundAttribute_Orig(Fixture.InternalsOfHeadXml, 0);
            var faster = Fixture.ParseFirstFoundAttribute_New1(Fixture.InternalsOfHeadXml, 0);
            if(orig.TotalLength != faster.TotalLength)
            {
                throw new Exception("TotalLength");
            }
            if (orig.Attribute.IsEmpty != faster.Attribute.IsEmpty)
            {
                throw new Exception("Attribute.IsEmpty");
            }
            if (!orig.Attribute.Name.SequenceEqual(faster.Attribute.Name))
            {
                throw new Exception("Attribute.Name");
            }
            if (!orig.Attribute.Value.SequenceEqual(faster.Attribute.Value))
            {
                throw new Exception("Attribute.Value");
            }
            if (!orig.Attribute.Prefix.SequenceEqual(faster.Attribute.Prefix))
            {
                throw new Exception("Attribute.Prefix");
            }

#else
            //BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Fixture).Assembly);

            var span = Fixture.AuxXml.AsSpan();
            for (var cc = 0; cc < 150000; cc++)
            {
                XmlSerializerDeserializer.Deserialize(span, out InfoContainer r);
            }
#endif
        }
    }

    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser]
    public class Fixture
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

        public static string AuxXml = @"
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

        public Fixture()
        {
            _ = Old();
            _ = New();
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



        public readonly string PartXml = @"          <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
<Email>example@example.com</Email>
</BaseInfo>";

        public static readonly string InternalsXml = @"<InfoContainer>
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
".Replace("\r\n", "");


        /*

        |   Method |      Mean |    Error |   StdDev |    Median | Allocated |
        |--------- |----------:|---------:|---------:|----------:|----------:|
        | OrigScan | 205.54 ns | 4.156 ns | 8.766 ns | 202.41 ns |         - |
        |  NewScan | 168.73 ns | 3.262 ns | 5.627 ns | 167.33 ns |         - |
        | NewScan2 | 147.11 ns | 2.457 ns | 2.178 ns | 147.02 ns |         - |
        | NewScan3 | 136.09 ns | 2.656 ns | 7.359 ns | 134.38 ns |         - |
        | NewScan4 |  56.07 ns | 1.207 ns | 3.464 ns |  54.76 ns |         - |



        */

        //[Benchmark]
        public ScanHeadResult1 OrigScan()
        {
            return ScanHeadOrig(PartXml.AsSpan());
        }

        //[Benchmark]
        public ScanHeadResult1 NewScan()
        {
            return ScanHeadNew(PartXml.AsSpan());
        }

        //[Benchmark]
        public ScanHeadResult1 NewScan2()
        {
            return ScanHeadNew2(PartXml.AsSpan());
        }

        //[Benchmark]
        public ScanHeadResult1 NewScan3()
        {
            return ScanHeadNew3(PartXml.AsSpan());
        }

        //[Benchmark]
        public ScanHeadResult1 NewScan4()
        {
            return ScanHeadNew4(PartXml.AsSpan());
        }

        private static ScanHeadResult1 ScanHeadNew4(ReadOnlySpan<char> fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var trimmed = fullnode.TrimStart();
            if (trimmed.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var index = fullnode.Length - trimmed.Length;
            var firstNonSpace = index;

            //ищем конец имени ноды
            var endOfNameIndex = trimmed.IndexOfAny(
                '/', '>', ' '
                );
            var nodeTypeLength = firstNonSpace + endOfNameIndex;

            //ищем конец головы
            var endOfHeadIndex = trimmed.IndexOf(
                '>'
                );
            var chm1 = trimmed[endOfHeadIndex - 1];
            var isBodyLess = chm1 == '/';
            index += endOfHeadIndex;

            return new ScanHeadResult1(
                fullnode.Slice(firstNonSpace, index + 1),
                fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1),
                isBodyLess
                );
        }

        private static ScanHeadResult1 ScanHeadNew3(ReadOnlySpan<char> fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var trimmed = fullnode.TrimStart();
            if (trimmed.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var index = fullnode.Length - trimmed.Length;
            var firstNonSpace = index;

            //ищем конец имени ноды
            var endOfNameIndex = trimmed.IndexOfAny(
                '/', '>', ' '
                );
            var nodeTypeLength = firstNonSpace + endOfNameIndex;
            index += endOfNameIndex;

            //ищем конец головы
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '>')
                {
                    var chm1 = fullnode[index - 1];
                    var isBodyLess = chm1 == '/';
                    //var isBodyLessIndex = isBodyLess ? 1 : 0;

                    return new ScanHeadResult1(
                        fullnode.Slice(firstNonSpace, index + 1),
                        fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1 /*- isBodyLessIndex*/),
                        isBodyLess
                        );
                }
                index++;
            }
        }

        private static ScanHeadResult1 ScanHeadNew2(ReadOnlySpan<char> fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var trimmed = fullnode.TrimStart();
            if(trimmed.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var index = fullnode.Length - trimmed.Length;
            var firstNonSpace = index;
            ////пропускаем пробелы
            //var firstNonSpace = 0;
            //while (true)
            //{
            //    if (index == fullnode.Length)
            //    {
            //        return new ScanHeadResult1();
            //    }

            //    var ch = fullnode[index];
            //    if (!char.IsWhiteSpace(ch))
            //    {
            //        firstNonSpace = index;
            //        break;
            //    }
            //    index++;
            //}

            //ищем конец имени ноды
            var nodeTypeLength = 0;
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '/' || ch == '>')
                {
                    nodeTypeLength = index;
                    break;
                }
                if (ch == ' ' || char.IsWhiteSpace(ch))
                {
                    nodeTypeLength = index;
                    index++;
                    break;
                }
                index++;
            }

            //ищем конец головы
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '>')
                {
                    var chm1 = fullnode[index - 1];
                    var isBodyLess = chm1 == '/';
                    //var isBodyLessIndex = isBodyLess ? 1 : 0;

                    return new ScanHeadResult1(
                        fullnode.Slice(firstNonSpace, index + 1),
                        fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1 /*- isBodyLessIndex*/),
                        isBodyLess
                        );
                }
                index++;
            }
        }

        private static ScanHeadResult1 ScanHeadNew(ReadOnlySpan<char> fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var index = 0;

            //пропускаем пробелы
            var firstNonSpace = 0;
            while (true)
            {
                if (index == fullnode.Length)
                {
                    return new ScanHeadResult1();
                }

                var ch = fullnode[index];
                if (!char.IsWhiteSpace(ch))
                {
                    firstNonSpace = index;
                    break;
                }
                index++;
            }

            //ищем конец имени ноды
            var nodeTypeLength = 0;
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '/' || ch == '>')
                {
                    nodeTypeLength = index;
                    break;
                }
                if (ch == ' ' || char.IsWhiteSpace(ch))
                {
                    nodeTypeLength = index;
                    index++;
                    break;
                }
                index++;
            }

            //ищем конец головы
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '>')
                {
                    var chm1 = fullnode[index - 1];
                    var isBodyLess = chm1 == '/';
                    //var isBodyLessIndex = isBodyLess ? 1 : 0;

                    return new ScanHeadResult1(
                        fullnode.Slice(firstNonSpace, index + 1),
                        fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1 /*- isBodyLessIndex*/),
                        isBodyLess
                        );
                }
                index++;
            }
        }

        public static ScanHeadResult1 ScanHeadOrig(ReadOnlySpan<char> fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult1();
            }

            var index = 0;

            //пропускаем пробелы
            var firstNonSpace = 0;
            while (true)
            {
                if (index == fullnode.Length)
                {
                    return new ScanHeadResult1();
                }

                var ch = fullnode[index];
                if (!char.IsWhiteSpace(ch))
                {
                    firstNonSpace = index;
                    break;
                }
                index++;
            }

            //ищем конец имени ноды
            var nodeTypeLength = 0;
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '/' || ch == '>')
                {
                    nodeTypeLength = index;
                    break;
                }
                if (char.IsWhiteSpace(ch))
                {
                    nodeTypeLength = index;
                    index++;
                    break;
                }
                index++;
            }

            //ищем конец головы
            while (true)
            {
                var ch = fullnode[index];
                if (ch == '>')
                {
                    var chm1 = fullnode[index - 1];
                    var isBodyLess = chm1 == '/';
                    //var isBodyLessIndex = isBodyLess ? 1 : 0;

                    return new ScanHeadResult1(
                        fullnode.Slice(firstNonSpace, index + 1),
                        fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1 /*- isBodyLessIndex*/),
                        isBodyLess
                        );
                }
                index++;
            }
        }

        public readonly ref struct ScanHeadResult1
        {
            public readonly bool IsEmpty;
            public readonly ReadOnlySpan<char> FullHead;
            public readonly ReadOnlySpan<char> NodeType;
            public readonly bool IsBodyless;

            public ScanHeadResult1()
            {
                IsEmpty = true;
                FullHead = ReadOnlySpan<char>.Empty;
                NodeType = ReadOnlySpan<char>.Empty;
                IsBodyless = true;
            }

            public ScanHeadResult1(ReadOnlySpan<char> fullHead, ReadOnlySpan<char> nodeType, bool isBodyless)
            {
                IsEmpty = FullHead.IsEmpty && nodeType.IsEmpty;
                FullHead = fullHead;
                NodeType = nodeType;
                IsBodyless = isBodyless;
            }
        }


/*

|        Method |     Mean |     Error |    StdDev | Allocated |
|-------------- |---------:|----------:|----------:|----------:|
| GetFirst_Orig | 3.945 us | 0.0768 us | 0.1386 us |         - |

|        Method |     Mean |     Error |    StdDev | Allocated |
|-------------- |---------:|----------:|----------:|----------:|
| GetFirst_Orig | 3.959 us | 0.0722 us | 0.1507 us |         - |
| GetFirst_New1 | 3.619 us | 0.0712 us | 0.1437 us |         - |

|        Method |     Mean |     Error |    StdDev | Allocated |
|-------------- |---------:|----------:|----------:|----------:|
| GetFirst_New1 | 3.712 us | 0.0634 us | 0.1296 us |         - |
| GetFirst_New2 | 1.442 us | 0.0229 us | 0.0357 us |         - |

*/
        //[Benchmark]
        public void GetFirst_Orig()
        {
            _ = GetFirst_Orig(InternalsXml.AsSpan());
        }

        //[Benchmark]
        public void GetFirst_New1()
        {
            _ = GetFirst_New1(InternalsXml.AsSpan());
        }

        //[Benchmark]
        public void GetFirst_New2()
        {
            _ = GetFirst_New2(InternalsXml.AsSpan());
        }

        public static XmlNode2 GetFirst_New2(
            ReadOnlySpan<char> nodes
            )
        {
            var length = GetFirstLength_New2(nodes);
            if(length == 0)
            {
                return new XmlNode2();
            }

            return new XmlNode2(nodes.Slice(0, length));
        }

        private static int GetFirstLength_New2(
            ReadOnlySpan<char> nodes
            )
        {
            var shr = ScanHeadNew4(nodes);
            if (shr.IsEmpty)
            {
                return 0;
            }
            if (shr.IsBodyless)
            {
                return 0;
            }

            //теперь сканируем до закрывающего тега
            var index = shr.FullHead.Length;
            while (true)
            {
                var sliced = nodes.Slice(index);
                var iof = sliced.IndexOf('<');
                index += iof;

                //возможно, это закрывающий тег, или открывающий дочерней ноды
                //TODO: или это камент, пока не поддерживается
                var ch1 = nodes[index + 1];
                if (ch1 == '/')
                {
                    //это закрывающий тег, надо сравнить его с нашим
                    var eq = MemoryExtensions.SequenceEqual(
                        shr.NodeType,
                        nodes.Slice(index + 2, shr.NodeType.Length)
                        );
                    if (!eq)
                    {
                        //не наша нода, битый XML?
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }
                    //сравним последний символ
                    if (nodes[index + 2 + shr.NodeType.Length] != '>')
                    {
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }

                    //успех, нашли закрывающий тег
                    return index + 2 + shr.NodeType.Length + 1;
                }
                else
                {
                    //это открывающий тег дочерней ноды
                    var innerNodes = nodes.Slice(index);
                    var childLength = GetFirstLength_New2(innerNodes);
                    //получили дочернюю ноду, пропускаем ее
                    index += childLength;
                }
            }
        }

        public static XmlNode2 GetFirst_New1(
            ReadOnlySpan<char> nodes
            )
        {
            var shr = ScanHeadNew4(nodes);
            if (shr.IsEmpty)
            {
                return new XmlNode2();
            }
            if (shr.IsBodyless)
            {
                return new XmlNode2();
            }

            //теперь сканируем до закрывающего тега
            var index = shr.FullHead.Length;
            while (true)
            {
                var sliced = nodes.Slice(index);
                var iof = sliced.IndexOf('<');
                index += iof;

                //возможно, это закрывающий тег, или открывающий дочерней ноды
                //TODO: или это камент, пока не поддерживается
                var ch1 = nodes[index + 1];
                if (ch1 == '/')
                {
                    //это закрывающий тег, надо сравнить его с нашим
                    var eq = MemoryExtensions.SequenceEqual(
                        shr.NodeType,
                        nodes.Slice(index + 2, shr.NodeType.Length)
                        );
                    if (!eq)
                    {
                        //не наша нода, битый XML?
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }
                    //сравним последний символ
                    if (nodes[index + 2 + shr.NodeType.Length] != '>')
                    {
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }

                    //успех, нашли закрывающий тег
                    return new XmlNode2(nodes.Slice(0, index + 2 + shr.NodeType.Length + 1));
                }
                else
                {
                    //это открывающий тег дочерней ноды
                    var innerNodes = nodes.Slice(index);
                    //var q = innerNodes.ToString();
                    var child = GetFirst_New1(innerNodes);
                    //получили дочернюю ноду, пропускаем ее
                    index += child.FullNode.Length;
                }
            }
        }

        public static XmlNode2 GetFirst_Orig(
            ReadOnlySpan<char> nodes
            )
        {
            var shr = ScanHeadNew4(nodes);
            if (shr.IsEmpty)
            {
                return new XmlNode2();
            }
            if (shr.IsBodyless)
            {
                return new XmlNode2();
            }

            //теперь сканируем до закрывающего тега
            var index = shr.FullHead.Length;
            while (true)
            {
                var ch0 = nodes[index];
                if (ch0 == '<')
                {
                    //возможно, это закрывающий тег, или открывающий дочерней ноды
                    //TODO: или это камент, пока не поддерживается
                    var ch1 = nodes[index + 1];
                    if (ch1 == '/')
                    {
                        //это закрывающий тег, надо сравнить его с нашим
                        var compareResult = MemoryExtensions.SequenceCompareTo(
                            shr.NodeType,
                            nodes.Slice(index + 2, shr.NodeType.Length)
                            );
                        if (compareResult != 0)
                        {
                            //не наша нода, битый XML?
                            throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                        }
                        //сравним последний символ
                        if (nodes[index + 2 + shr.NodeType.Length] != '>')
                        {
                            throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                        }

                        //успех, нашли закрывающий тег
                        return new XmlNode2(nodes.Slice(0, index + 2 + shr.NodeType.Length + 1));
                    }
                    else
                    {
                        //это открывающий тег дочерней ноды
                        var innerNodes = nodes.Slice(index);
                        //var q = innerNodes.ToString();
                        var child = GetFirst_Orig(innerNodes);
                        //получили дочернюю ноду, пропускаем ее
                        index += child.FullNode.Length;
                    }
                }
                else
                {
                    index++;
                }
            }
        }



        public readonly static string InternalsOfHeadXml = @"    xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info""    ";

/*
|                          Method |     Mean |   Error |  StdDev | Allocated |
|-------------------------------- |---------:|--------:|--------:|----------:|
| DoParseFirstFoundAttribute_Orig | 200.3 ns | 3.86 ns | 4.59 ns |         - |

|                          Method |      Mean |    Error |   StdDev | Allocated |
|-------------------------------- |----------:|---------:|---------:|----------:|
| DoParseFirstFoundAttribute_Orig | 200.31 ns | 4.005 ns | 4.285 ns |         - |
| DoParseFirstFoundAttribute_New1 |  61.22 ns | 0.990 ns | 0.926 ns |         - | 
 
 
*/

        //[Benchmark]
        public void DoParseFirstFoundAttribute_Orig()
        {
            _ = ParseFirstFoundAttribute_Orig(InternalsOfHeadXml.AsSpan(), 0);
        }

        //[Benchmark]
        public void DoParseFirstFoundAttribute_New1()
        {
            _ = ParseFirstFoundAttribute_New1(InternalsOfHeadXml.AsSpan(), 0);
        }


        public static AttributeProcessResult ParseFirstFoundAttribute_New1(
            ReadOnlySpan<char> internalsOfHead,
            int iindex
            )
        {
            var trimmed = internalsOfHead.Slice(iindex).TrimStart();
            var trimmedLength = internalsOfHead.Length - trimmed.Length;

            var iofa0 = trimmed.IndexOfAny("/>:".AsSpan());
            var c = trimmed[iofa0];
            if (c == '/' || c == '>')
            {
                //нода закрылась, атрибутов нету
                return new AttributeProcessResult();
            }

            var prefix = internalsOfHead.Slice(trimmedLength, iofa0);
            //var afterPrefixIndex = trimmedLength + iofa0 + 1;
            trimmed = trimmed.Slice(iofa0 + 1);

            var iofa1 = trimmed.IndexOfAny("=".AsSpan());
            var name = trimmed.Slice(0, iofa1);
            //var afterNameIndex = afterPrefixIndex + iofa1;
            trimmed = trimmed.Slice(iofa1 + 1);

            var iofa2 = trimmed.IndexOfAny("\"".AsSpan());
            //найдено начало значения
            //var startValueIndex = afterNameIndex + iofa2 + 1;
            trimmed = trimmed.Slice(iofa2 + 1);

            var iofa3 = trimmed.IndexOfAny("\"".AsSpan());
            //найден конец значения
            var value = trimmed.Slice(0, iofa3);
            //var endValueIndex = startValueIndex + iofa3;

            return new AttributeProcessResult(
                new ParsedAttribute(prefix, name, value),
                trimmedLength + iofa0 + iofa1 + iofa2 + iofa3 + 4 - iindex
                );
        }

        public static AttributeProcessResult ParseFirstFoundAttribute_Orig(
            ReadOnlySpan<char> internalsOfHead,
            int iindex
            )
        {
            var index = iindex;

            //пропускаем пробелы
            while (char.IsWhiteSpace(internalsOfHead[index]))
            {
                index++;
            }

            var startIndex = index;

            bool foundFirstQuote = false;
            var prefix = ReadOnlySpan<char>.Empty; //before :
            var name = ReadOnlySpan<char>.Empty; //after :
            var value = ReadOnlySpan<char>.Empty; //inside ""

            while (true)
            {
                var c = internalsOfHead[index];

                if ((c == '/' || c == '>') && !foundFirstQuote)
                {
                    //нода закрылась, атрибутов нету
                    return new AttributeProcessResult();
                }
                else if (c == ':' && !foundFirstQuote)
                {
                    prefix = internalsOfHead.Slice(startIndex, index - startIndex);
                    startIndex = index + 1;
                }
                else if (c == '=' && !foundFirstQuote)
                {
                    name = internalsOfHead.Slice(startIndex, index - startIndex);
                    startIndex = index + 1;
                }
                else if (c == '"' && !foundFirstQuote)
                {
                    //найдено начало значения
                    startIndex = index + 1;
                    foundFirstQuote = true;
                }
                else if (c == '"' && foundFirstQuote)
                {
                    //найден конец значения
                    value = internalsOfHead.Slice(startIndex, index - startIndex);
                }

                index++;

                if (!value.IsEmpty)
                {
                    break;
                }
            }

            return new AttributeProcessResult(
                new ParsedAttribute(prefix, name, value),
                index - iindex
                );
        }


        /*


        |           Method |      Mean |     Error |    StdDev | Allocated |
        |----------------- |----------:|----------:|----------:|----------:|
        |    RefStructTest | 0.1167 ns | 0.0319 ns | 0.0327 ns |         - |
        | ReturnStructTest | 5.4232 ns | 0.1375 ns | 0.1528 ns |         - |


        */

        //[Benchmark]
        //public ReturnStruct RefStructTest()
        //{
        //    ReturnStruct result = new();
        //    RefMethod0(ref result);
        //    return result;
        //}
        //private void RefMethod0(ref ReturnStruct result)
        //{
        //    RefMethod1(ref result);
        //    if (result.Field1 > 0)
        //    {
        //        return;
        //    }
        //    result = new ReturnStruct();
        //}
        //private void RefMethod1(ref ReturnStruct result)
        //{
        //    RefMethod2(ref result);
        //    if (result.Field1 > 0)
        //    {
        //        return;
        //    }
        //    result = new ReturnStruct();
        //}
        //private void RefMethod2(ref ReturnStruct result)
        //{
        //    RefMethod3(ref result);
        //    if(result.Field1 > 0)
        //    {
        //        return;
        //    }
        //    result = new ReturnStruct();
        //}
        //private void RefMethod3(ref ReturnStruct result)
        //{
        //    result = new ReturnStruct(0, 0, 0, 0);
        //}


        //[Benchmark]
        //public ReturnStruct ReturnStructTest()
        //{
        //    return ReturnMethod0();
        //}

        //private ReturnStruct ReturnMethod0()
        //{
        //    var r = ReturnMethod1();
        //    if (r.Field1 > 0)
        //    {
        //        return r;
        //    }
        //    return new ReturnStruct();
        //}
        //private ReturnStruct ReturnMethod1()
        //{
        //    var r = ReturnMethod2();
        //    if (r.Field1 > 0)
        //    {
        //        return r;
        //    }
        //    return new ReturnStruct();
        //}
        //private ReturnStruct ReturnMethod2()
        //{
        //    var r = ReturnMethod3();
        //    if(r.Field1 > 0)
        //    {
        //        return r;
        //    }
        //    return new ReturnStruct();
        //}
        //private ReturnStruct ReturnMethod3()
        //{
        //    return new ReturnStruct(0, 0, 0, 0);
        //}


        //public readonly ref struct ReturnStruct
        //{
        //    public readonly long Field1;
        //    public readonly long Field2;
        //    public readonly long Field3;
        //    public readonly long Field4;

        //    public ReturnStruct(int field1, int field2, int field3, int field4)
        //    {
        //        Field1 = field1;
        //        Field2 = field2;
        //        Field3 = field3;
        //        Field4 = field4;
        //    }
        //}

        //[Benchmark]
        //public int FieldSpan()
        //{
        //    return new Field().Do();
        //}
        //[Benchmark]
        //public int LocalSpan()
        //{
        //    return new Local().Do();
        //}

        //private readonly ref struct Field
        //{
        //    private readonly ReadOnlySpan<char> _f = "1234567890q".AsSpan();
        //    public Field()
        //    {
        //    }

        //    public readonly int Do()
        //    {
        //        var sum = 0;
        //        for (var i = 0; i < 300; i++)
        //        {
        //            sum += GetSpanLength(_f);
        //        }
        //        return sum;
        //    }
        //    [MethodImpl(MethodImplOptions.NoInlining)]
        //    private static int GetSpanLength(ReadOnlySpan<char> span) => span.Length;
        //}

        //private readonly ref struct Local
        //{
        //    public readonly int Do()
        //    {
        //        var sum = 0;
        //        for (var i = 0; i < 300; i++)
        //        {
        //            sum += GetSpanLength("1234567890q".AsSpan());
        //        }
        //        return sum;
        //    }
        //    [MethodImpl(MethodImplOptions.NoInlining)]
        //    private static int GetSpanLength(ReadOnlySpan<char> span) => span.Length;
        //}


    }

}