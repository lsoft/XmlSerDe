# XmlSerDe

Allocation free XML deserializer based on C# incremental source generators..

## Status

Status: prototype.

Restrictions now:

- do not support XML headers
- do not support CDATA
- do not support XML comments in the XML document
- do not support malformed XML documents

# Results

For XML document:

```xml
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
```

the results are:

```
|     Method |     Mean |    Error |   StdDev | Ratio |   Gen0 | Allocated | Alloc Ratio |
|----------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| System.Xml | 15.92 us | 0.234 us | 0.219 us |  1.00 | 3.9063 |   16392 B |        1.00 |
|   XmlSerDe | 11.80 us | 0.112 us | 0.137 us |  0.74 | 0.1678 |     736 B |        0.04 |
```
