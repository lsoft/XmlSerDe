# XmlSerDe

Allocation free XML deserializer based on C# incremental source generators (ISG). Because of ISG no performance issues occurs when working with big codebases.

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
|     Method |     Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
|----------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| System.Xml | 15.92 us | 0.234 us | 0.219 us |  1.00 | 3.9063 |   16392 B |        1.00 |
|   XmlSerDe | 11.80 us | 0.112 us | 0.137 us |  0.74 | 0.1678 |     736 B |        0.04 |
```

the code:

```C#

    public InfoContainer XmlSerDe()
    {
        XmlSerializerDeserializer.Deserialize(XmlText.AsSpan(), out InfoContainer r);
        return r;
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
