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
|                    Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
|-------------------------- |----------:|----------:|----------:|-------:|----------:|
|   'Serialize: System.Xml' |  8.320 us | 0.1332 us | 0.1246 us | 3.3569 |   14064 B |
|     'Serialize: XmlSerDe' |  5.041 us | 0.1006 us | 0.1235 us | 1.5640 |    6569 B |
| 'Deserialize: System.Xml' | 15.838 us | 0.2321 us | 0.3099 us | 3.9063 |   16392 B |
|   'Deserialize: XmlSerDe' | 12.033 us | 0.2402 us | 0.2571 us | 0.1678 |     736 B |

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
        using var ms = new MemoryStream();
        XmlSerializerDeserializer.Serialize(ms, subject, false);
        var xml = Encoding.UTF8.GetString(ms.ToArray());
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
    [XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.PerformanceTests.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
    public partial class XmlSerializerDeserializer
    {
    }
```

## Alternatives

You may be interested in the following project: https://github.com/ZingBallyhoo/StackXML
