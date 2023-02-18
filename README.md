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
|     'Serialize: System.Xml' |  7.734 us | 0.0516 us | 0.0483 us | 6.6986 |   14064 B |
|       'Serialize: XmlSerDe' |  2.581 us | 0.0190 us | 0.0177 us | 3.2921 |    6889 B |
|   'Deserialize: System.Xml' | 13.814 us | 0.0391 us | 0.0327 us | 7.8125 |   16392 B |
|     'Deserialize: XmlSerDe' | 10.664 us | 0.0194 us | 0.0172 us | 0.3510 |     736 B | <-- allocations!

```

the code:

```C#

    public InfoContainer Deserialize(ReadOnlySpan<char> xml)
    {
        XmlSerializerDeserializer.Deserialize(xml, out InfoContainer r);
        return r;
    }

    public string Serialize(InfoContainer subject)
    {
        var sb = new StringBuilder();
        XmlSerializerDeserializer.Serialize(sb, subject, false);
        var xml = sb.ToString();
        return xml;
    }


//...

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

## Alternatives

You may be interested in the following project: https://github.com/ZingBallyhoo/StackXML
