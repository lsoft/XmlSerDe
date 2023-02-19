# XmlSerDe

Allocation free XML serializer/deserializer based on C# incremental source generators (ISG). Because of ISG no performance issues occurs when working with big codebases.

## Status

Status: prototype.

Restrictions now:

- Do not support CDATA.
- Do not support XML comments in the XML document.
- Do not support malformed XML documents. No guard from malformed XML documents exists. Do not use it with potentially malicious XML documents.
- Serialized object must have a parameterless contructor and must be visible from the deserialization class.

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
BenchmarkDotNet=v0.13.5, OS=Windows 10 (10.0.19045.2604/22H2/2022Update)
AMD Ryzen 7 4700U with Radeon Graphics, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.200-preview.22628.1
  [Host]   : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT AVX2
  .NET 6.0 : .NET 6.0.14 (6.0.1423.7309), X64 RyuJIT AVX2

Job=.NET 6.0  Runtime=.NET 6.0

|                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|---------------------------- |----------:|----------:|----------:|-------:|----------:|
|     'Serialize: System.Xml' |  7.702 us | 0.0802 us | 0.0750 us | 6.6986 |   14064 B |
|       'Serialize: XmlSerDe' |  2.591 us | 0.0051 us | 0.0045 us | 3.3073 |    6921 B |
| 'Serialize: XmlSerDe (est)' |  2.585 us | 0.0107 us | 0.0095 us | 2.4185 |    5065 B |
|   'Deserialize: System.Xml' | 13.790 us | 0.0557 us | 0.0465 us | 7.8125 |   16392 B |
|     'Deserialize: XmlSerDe' | 10.848 us | 0.0396 us | 0.0351 us | 0.3510 |     736 B |
```

PTAL on few points:

1. `(est)` test do premature estimation of result XML document length, and allocate the buffer of appropriate size. Serialization with estimation on may be a bit slower than a regular one, but it allocate less.
2. Deserialization process allocate only 5% memory in comparison to the standard serializer.

the code:

```C#

    public InfoContainer Deserialize(ReadOnlySpan<char> xml)
    {
        XmlSerializerDeserializer.Deserialize(xml, out InfoContainer r);
        return r;
    }

    public string Serialize(InfoContainer subject)
    {
        var dsbe = new DefaultStringBuilderExhauster();
        XmlSerializerDeserializer.Serialize(dsbe, subject, false);
        var xml = dsbe.ToString();
        return xml;
    }

    public string Serialize_Est(InfoContainer subject)
    {
        //estimation phase:
        var dlee = new DefaultLengthEstimatorExhauster();
        XmlSerializerDeserializer.Serialize(dlee, DefaultObject, false);
        var estimateXmlLength = dlee.EstimatedTotalLength;

        //serialization phase:
        var dsbe = new DefaultStringBuilderExhauster(
            new StringBuilder(estimateXmlLength)
            );
        XmlSerializerDeserializer.Serialize(dsbe, subject, false);
        var xml = dsbe.ToString();
        return xml;
    }

//...

    [XmlExhauster(typeof(DefaultLengthEstimatorExhauster))]
    [XmlExhauster(typeof(DefaultStringBuilderExhauster))]
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

## Serialization

TODO in general

TODO: what is exhauster; embedded exhauster; custom exhauster; using custom exhauster to change serialization format (for DateTime for example).


## Alternatives

You may be interested in the following project: https://github.com/ZingBallyhoo/StackXML
