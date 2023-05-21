# XmlSerDe

Allocation free XML serializer/deserializer based on C# incremental source generators (ISG). Because of ISG no performance issues occurs when working with big codebases.

## Status

Status: prototype.

Restrictions now:

- Do not support CDATA.
- Do not support XML comments in the XML document.
- Do not support malformed XML documents. No guard from malformed XML documents exists. Do not use it with potentially malicious XML documents.
- Serialized object must have a parameterless contructor and must be visible from the deserialization class.
- Serialized fields/properties must have accessible setter for deserialization.
- Only `List<T>` and `T[]` supports as collections.

## Features

- Tuned for no (or minimum) memory allocation.
- Deserializer can accept a custom object factories which is useful for reuse already allocated objects (see `XmlFactory` attribute in the performance section below).

# Performance

For the following XML document:

```xml
<InfoContainer>
    <InfoCollection>
        <BaseInfo xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="Derived3Info">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="Derived1Info">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="Derived2Info">
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
```

the performance is:

```
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

|                         Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|      'Deserialize: System.Xml' | 7.265 us | 0.0404 us | 0.0359 us | 1.2741 | 0.0610 |   16072 B |
|        'Deserialize: XmlSerDe' | 6.417 us | 0.0210 us | 0.0186 us | 0.0610 |      - |     824 B |
```

PTAL on few points:

1. `(est)` test do premature estimation of result XML document length, and allocate the buffer of appropriate size. Serialization with estimation on may be a bit slower than a regular one, but it allocate less.
2. `(stream)` test serializes the data into the binary for sending into stream and\or network. For test purposes we do not send the data anywhere. Low allocations are possible because of `ArrayPool<>.Rent` use.
3. Deserialization process allocate only 5% memory in comparison to the standard serializer.

the code:

```C#

    public InfoContainer Deserialize(ReadOnlySpan<char> xml)
    {
        XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer r);
        return r;
    }

    public string Serialize()
    {
        var dsbe = new DefaultStringBuilderExhauster();
        XmlSerializerDeserializer.Serialize(dsbe, DefaultObject, false);
        var xml = dsbe.ToString();
        return xml;
    }

    public string Serialize_Est()
    {
        //estimation phase:
        var dlee = new DefaultLengthEstimatorExhauster();
        XmlSerializerDeserializer.Serialize(dlee, DefaultObject, false);
        var estimateXmlLength = dlee.EstimatedTotalLength;

        //serialization phase:
        var dsbe = new DefaultStringBuilderExhauster(
            new StringBuilder(estimateXmlLength)
            );
        XmlSerializerDeserializer.Serialize(dsbe, DefaultObject, false);
        var xml = dsbe.ToString();
        return xml;
    }

    public void Serialize_ToStream_Test()
    {
        var be = new Utf8BinaryExhausterEmpty(
            );
        XmlSerializerDeserializer.Serialize(be, DefaultObject, false);
    }

//...

    public class Utf8BinaryExhausterEmpty : Utf8BinaryExhauster
    {
        protected override void Write(byte[] data, int length)
        {
            //nothing to do in tests
        }
    }

    [XmlExhauster(typeof(DefaultLengthEstimatorExhauster))]
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
    [XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived1Info))]
    [XmlSubject(typeof(Derived1Info), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived2Info))]
    [XmlSubject(typeof(Derived2Info), false)]
    [XmlDerivedSubject(typeof(BaseInfo), typeof(Derived3Info))]
    [XmlSubject(typeof(Derived3Info), false)]
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.Tests.Complex.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializer
    {
    }
```

## Serialization/deserialization class

TODO in general

TODO: what attributes means

## Deserialization

TODO in general

TODO: what is injector; embedded injector; custom injector; using custom injector to change deserialization format (for DateTime for example).

## Serialization

TODO in general

TODO: what is exhauster; embedded exhauster; custom exhauster; using custom exhauster to change serialization format (for DateTime for example).


## Alternatives

You may be interested in the following project: https://github.com/ZingBallyhoo/StackXML
