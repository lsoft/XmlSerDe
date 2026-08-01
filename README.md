# XmlSerDe

Allocation-free XML serializer/deserializer based on C# incremental source generators (ISG). Because generation happens at compile time, there is no runtime reflection cost and no performance degradation as the number of serialized types grows.

XmlSerDe's purpose is **POCO ↔ XML data binding**: mapping plain C# classes to XML and back, format-compatible with `System.Xml.Serialization` for the primitives, collections, and `xsi:type` polymorphism it supports. It targets that data-binding scenario specifically rather than being a general-purpose XML processor — see [Out of scope by design](#out-of-scope-by-design) for what that excludes and why.

## Status

The API and behavior may still change between releases.

## Solution structure

| Project | Target | Role |
|---------|--------|------|
| **XmlSerDe.Common** | netstandard2.0 | Attributes, `IInjector` / `IExhauster` contracts, and `XmlNode2` — a low-allocation XML node parser over `ReadOnlySpan<char>`. |
| **XmlSerDe.Components** | net7.0 | Default runtime implementations: injectors and exhausters. |
| **XmlSerDe.Generator** | netstandard2.0 (Roslyn analyzer) | Incremental source generator that emits serialization/deserialization code at compile time. |
| **XmlSerDe.Tests** | net7.0 | Functional tests (xUnit). |
| **XmlSerDe.PerformanceTests** | net7.0 | BenchmarkDotNet benchmarks vs `System.Xml.Serialization`. |

**Dependency flow:** consumer app → `XmlSerDe.Common` + `XmlSerDe.Components` + `XmlSerDe.Generator` (analyzer).

## Getting started

### 1. Add project references

```xml
<ProjectReference Include="..\XmlSerDe.Common\XmlSerDe.Common.csproj" />
<ProjectReference Include="..\XmlSerDe.Components\XmlSerDe.Components.csproj" />
<ProjectReference Include="..\XmlSerDe.Generator\XmlSerDe.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

### 2. Define your types

```csharp
public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; }
    public List<OrderLine> Lines { get; set; }
}

public class OrderLine
{
    public string Product { get; set; }
    public int Quantity { get; set; }
}
```

### 3. Declare a partial serializer class

```csharp
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;

[XmlSubject(typeof(OrderLine), false)]
[XmlSubject(typeof(Order), true)]   // true = root type (public entry point)
public partial class OrderSerializer
{
}
```

The class **must** be `partial`. On build, the generator emits `OrderSerializer.g.cs` with `Serialize` and `Deserialize` methods.

### 4. Serialize

```csharp
var exhauster = new DefaultStringBuilderExhauster();
OrderSerializer.Serialize(exhauster, order, appendXmlHead: false);
string xml = exhauster.ToString();
```

### 5. Deserialize

```csharp
OrderSerializer.Deserialize(
    DefaultInjector.Instance,
    xml.AsSpan(),
    out Order result);
```

If the input includes an XML declaration (`<?xml ...?>`), strip it first:

```csharp
var body = BuiltinCodeHelper.CutXmlHead(fullXml.AsSpan());
OrderSerializer.Deserialize(DefaultInjector.Instance, body, out Order result);
```

## Serialization/deserialization class

You register types and configure code generation by decorating a single `partial` class with attributes from `XmlSerDe.Common`. All attributes can be applied multiple times to the same class.

### `[XmlSubject(typeof(T), isRoot)]`

Registers a type for serialization and deserialization.

| Parameter | Meaning |
|-----------|---------|
| `SubjectType` | The CLR type to handle. Every member type used in the object graph must be registered before it appears in another type. |
| `IsRoot` | `true` — generates a public `Deserialize(injector, xml, out T)` and root `Serialize(exh, obj, appendXmlHead)` for this type. Typically exactly one root per serializer class. |

### `[XmlDerivedSubject(typeof(Base), typeof(Derived))]`

Enables polymorphic deserialization via `xsi:type`. The base type must already have `[XmlSubject]`, and the derived type must be concrete and registered with its own `[XmlSubject]`. Abstract bases require at least one derived registration.

### `[XmlExhauster(typeof(T))]`

Registers an `IExhauster` implementation. The generator emits a `Serialize` overload for each registered exhauster. If omitted, `DefaultStringBuilderExhauster` is used automatically.

### `[XmlInjector(typeof(T))]`

Registers an `IInjector` implementation. The generator emits a `Deserialize` overload for each registered injector. If omitted, `DefaultInjector` is used automatically.

### `[XmlFactory(typeof(T), invocationStatement)]`

Replaces `new T()` during deserialization with a custom C# expression. Useful for object pooling and reuse of already-allocated instances.

```csharp
[XmlFactory(typeof(InfoContainer), "global::MyApp.CachedInfoContainer.Reuse()")]
```

The factory type must provide a `Reset()`-style method that clears state before reuse. See `CachedInfoContainer` in `XmlSerDe.Tests/Complex/Subject/InfoContainer.cs`.

### Example: polymorphic serializer

```csharp
[XmlSubject(typeof(BaseInfo), false)]
[XmlDerivedSubject(typeof(BaseInfo), typeof(Derived1Info))]
[XmlSubject(typeof(Derived1Info), false)]
[XmlSubject(typeof(InfoContainer), true)]
public partial class MySerializer { }
```

Produces XML compatible with `System.Xml.Serialization` polymorphism:

```xml
<BaseInfo xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="Derived1Info">
  <BasePersonificationInfo>my string</BasePersonificationInfo>
</BaseInfo>
```

## Deserialization

### Injector

An **injector** (`IInjector`) parses primitive/builtin values from XML nodes. It is the deserialization counterpart to an exhauster.

For each supported builtin type, `IInjector` defines:

- `Parse(ref XmlDeserializeSettings, fullNode, xmlnsAttributeName, out T)` — expects the XSD wrapper element (`<dateTime>`, `<int>`, etc.) inside the property element.
- `ParseBody(body, out T)` — parses only the inner text.

`DefaultInjector` (singleton: `DefaultInjector.Instance`):

- Validates the declared XSD element name, then delegates to `ParseBody`.
- Uses standard `*.Parse` methods for numeric types, `DateTime.Parse`, `Guid.Parse`, etc.
- **Culture-invariant:** every numeric and `DateTime` parse path uses `CultureInfo.InvariantCulture` explicitly, matching XSD's fixed lexical space regardless of the ambient thread culture.
- **Strings:** supports CDATA blocks (concatenates multiple), then `WebUtility.HtmlDecode`.
- **Booleans:** `"true"` / `"false"`.

### Custom injector

Implement `IInjector` and register it with `[XmlInjector(typeof(MyInjector))]`. Override `ParseBody` to change format — for example, a fixed `DateTime` format:

```csharp
public class IsoDateInjector : DefaultInjector
{
    public new void ParseBody(ReadOnlySpan<char> body, out DateTime result)
    {
        result = DateTime.ParseExact(body, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
```

The generator emits a `Deserialize(MyInjector inj, ReadOnlySpan<char> xml, out TRoot)` overload that routes all builtin parsing through your injector.

### XmlNode2

`XmlNode2` in `XmlSerDe.Common` is a `ref struct` that walks XML without allocating DOM nodes. The root `Deserialize` overload builds `XmlDeserializeSettings` from heuristics (whether comments or CDATA blocks are likely present) and iterates child nodes via `XmlNode2.GetFirst`.

Its attribute parser follows XML 1.0's actual grammar rather than a narrow subset:

- **Quoting:** `AttValue` may be delimited by either `"` or `'` (the closing quote must match the opening one).
- **Namespace prefix optional:** unprefixed attributes (e.g. `id="1"`) are recognized alongside prefixed ones (e.g. `p3:type="..."`).
- **Whitespace:** the XML `S` production (`#x20 | #x9 | #xD | #xA`) is honored between the element name and its attributes, not just a literal space.
- **`>` inside attribute values:** per XML 1.0 §2.4, `AttValue` only requires escaping `<`, `&`, and the matching quote character — `>` is legal unescaped (e.g. `<Foo attr="1>2">`). The tag-head scan is quote-aware, so it doesn't mistake such a `>` for the tag's actual close.
- **Attribute-value normalization (XML 1.0 §3.3.3):** entity/character references (`&amp;`, `&#49;`) are decoded via the same `WebUtility.HtmlDecode` convention used for element text, and literal tab/CR/LF characters are collapsed to a single space — but a character reference that expands to whitespace (e.g. `&#10;`) is inserted verbatim and is *not* collapsed, matching the spec's normalization algorithm. Allocates only when a reference or literal tab/CR/LF is actually present.

`BuiltinCodeHelper.CutXmlHead` strips the full XML prolog (XML 1.0 §2.8: `XMLDecl? Misc* (doctypedecl Misc*)?`), not just the `<?xml ...?>` declaration — leading comments, other processing instructions (e.g. `<?xml-stylesheet ...?>`), and a `<!DOCTYPE ...>` declaration (including one with an internal subset containing its own `>` characters) are all skipped via `XmlNode2.SkipPrologMisc`, in any order/combination.

## Serialization

### Exhauster

An **exhauster** (`IExhauster`) is the output sink for serialized data. For each supported builtin type it provides `Append(T)` and `Append(T?)`, plus:

- `Append(string? value)` — raw append.
- `AppendEncoded(string? value)` — validates then HTML-encodes then appends (used for `string` builtins).

Null nullable values (including nullable value types like `int?`, `DateTime?`) are skipped entirely on serialize rather than emitting an empty tag.

**Culture-invariant formatting:** numeric and `DateTime` values are formatted via `ISpanFormattable.TryFormat` into a stack buffer with `CultureInfo.InvariantCulture` (falling back to an invariant-culture `ToString` for larger values), so output matches XSD's fixed lexical space regardless of the ambient thread culture. `Guid` uses the same stack-buffer `TryFormat` path — note that `StringBuilder` has no `Append(Guid)` overload, so appending one directly would silently bind to `Append(object?)` and box.

**Well-formedness guard:** `AppendEncoded` calls `XmlCharGuard.EnsureValidXmlChars` before encoding. `WebUtility.HtmlEncode` escapes `<`, `>`, `&`, `"`, `'` but doesn't know about XML's `Char` production (XML 1.0 §2.2), which forbids most C0 control characters, unpaired surrogates, and `U+FFFE`/`U+FFFF` outright — there is no legal escape for these in XML. String content containing them throws `ArgumentException` instead of silently producing not-well-formed output. Legal whitespace (tab/CR/LF) is allowed through.

### Built-in exhausters

| Exhauster | Purpose |
|-----------|---------|
| `DefaultStringBuilderExhauster` | Writes to a `StringBuilder`. Optional pre-sized constructor. Default `DateTime` format: `yyyy-MM-ddTHH:mm:ss.fffffffK`. Not thread-safe. |
| `DefaultLengthEstimatorExhauster` | Counts estimated character length without building output. Exposes `EstimatedTotalLength` — use to pre-size a `StringBuilder` and reduce reallocations. |
| `Utf8BinaryExhauster` (abstract) | Converts values to UTF-8 bytes. Small values use an internal buffer; larger values rent from `ArrayPool<byte>`. Subclass and implement `Write(byte[] data, int length)` to send data to a stream or network. |

### Two-phase serialization (length estimation)

```csharp
// Estimation phase
var estimator = new DefaultLengthEstimatorExhauster();
OrderSerializer.Serialize(estimator, order, false);
var estimatedLength = estimator.EstimatedTotalLength;

// Serialization phase with pre-sized buffer
var exhauster = new DefaultStringBuilderExhauster(new StringBuilder(estimatedLength));
OrderSerializer.Serialize(exhauster, order, false);
string xml = exhauster.ToString();
```

Estimation may be slightly slower than a single pass, but allocates less because the `StringBuilder` does not need to grow.

### Stream serialization

```csharp
public class StreamExhauster : Utf8BinaryExhauster
{
    private readonly Stream _stream;
    public StreamExhauster(Stream stream) => _stream = stream;

    protected override void Write(byte[] data, int length)
        => _stream.Write(data, 0, length);
}
```

### Custom exhauster

Implement `IExhauster` and register with `[XmlExhauster(typeof(MyExhauster))]`. Override `Append` methods to customize output format.

Set `appendXmlHead: true` on `Serialize` to prepend `<?xml version="1.0" encoding="utf-8"?>`.

## Supported types

### Builtin primitives

XML element names follow XSD conventions:

| C# type | XML inner element |
|---------|-------------------|
| `DateTime` / `DateTime?` | `dateTime` |
| `Guid` / `Guid?` | `guid` |
| `bool` / `bool?` | `boolean` |
| `sbyte` / `sbyte?` | `byte` |
| `byte` / `byte?` | `unsignedByte` |
| `short` / `short?` | `short` |
| `ushort` / `ushort?` | `unsignedShort` |
| `int` / `int?` | `int` |
| `uint` / `uint?` | `unsignedInt` |
| `long` / `long?` | `long` |
| `ulong` / `ulong?` | `unsignedLong` |
| `decimal` / `decimal?` | `decimal` |
| `string` | `string` (HTML-encoded on serialize) |

### Complex types

- Classes registered with `[XmlSubject]`
- **Enums** — serialized as `<EnumTypeName>value</EnumTypeName>`. The generator knows every declared member at compile time, so it emits a `switch` over them on serialize and a chain of span `SequenceEqual` comparisons on deserialize, rather than `Enum.ToString()` / `Enum.Parse` — both of which go through reflection, and `Enum.Parse` additionally boxes its `object` return on every call. `Enum.ToString()` / `Enum.Parse` remain as the fallback arm for values that match no declared member (undefined numeric values, `[Flags]` combinations), so behavior is unchanged for those.
- **Inheritance** — via `[XmlDerivedSubject]` and `xsi:type`
- **Collections** — `List<T>` and `T[]` only

### Members

- Public fields and properties (including inherited) with accessible setters
- `[XmlIgnore]` properties are skipped
- Private and protected members are skipped
- XML element names match C# type and property names (not configurable)

## Limitations

- **No CDATA serialization** (CDATA deserialization for strings is partially supported in `DefaultInjector`).
- **No malformed-XML *input* protection** — the deserializer does not validate well-formedness of its input; do not use with untrusted input. (Serialization *output*, by contrast, is guarded: `AppendEncoded` rejects string content containing characters illegal per XML 1.0's `Char` production — see [`XmlCharGuard`](#exhauster).)
- **Parameterless constructor required** unless `[XmlFactory]` is used.
- Serialized types must be visible to the serializer partial class.
- Members need accessible setters for deserialization.
- **Only `List<T>` and `T[]`** as collections.
- The serializer class must be `partial`.
- Unknown member types cause a compile-time generator error.
- Multi-argument generics (e.g. `Dictionary<K,V>`) are not supported.

See also [Out of scope by design](#out-of-scope-by-design) for XML 1.0 features that aren't limitations to be lifted later, but deliberate consequences of targeting the POCO ↔ XML data-binding scenario.

## Out of scope by design

XmlSerDe targets POCO ↔ XML data binding, not general-purpose XML processing. The following XML 1.0 / XML Namespaces features are consequences of that scope, not oversights — each is unlikely to matter for typical data-transfer XML (including everything `System.Xml.Serialization` itself produces for the primitives, collections, and polymorphism XmlSerDe supports), but matters for interop with documents from other kinds of XML producers.

- **No DTD support.** A `<!DOCTYPE ...>` in the prolog is skipped over, not parsed — so any custom general entities it declares are not resolved. Only the five predefined XML entities (`&amp;`, `&lt;`, `&gt;`, `&apos;`, `&quot;`) plus numeric character references (`&#49;`, `&#x31;`) are understood (and, leniently, HTML5 named entities like `&nbsp;` via `WebUtility.HtmlDecode`, which technically aren't legal in bare XML without a DTD declaring them). There's also no DTD-based content validation and no fetching of external DTDs.
- **No general XML Namespaces support.** Only one namespace is special-cased: `xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"` for `xsi:type` polymorphism (the prefix itself is flexible — deserialize resolves whichever prefix is actually bound to that URI). Beyond that, element and attribute names are compared as literal text, prefix included; there's no general prefix-to-URI resolution or default-namespace (`xmlns="uri"`) handling.
- **No XML-attribute data binding.** Every serialized member becomes a child element; there's no equivalent of `System.Xml.Serialization`'s `[XmlAttribute]` or `[XmlText]`. (`[XmlIgnore]` is the exception — XmlSerDe recognizes `System.Xml.Serialization.XmlIgnoreAttribute` directly, so it can be reused as-is.)
- **`xml:space`, `xml:lang`, `xml:base` are not interpreted.** In practice this rarely matters for `xml:space`: text content is always preserved verbatim regardless (matching the XML default, `xml:space="preserve"`) — but `xml:space="default"`, which would opt back into whitespace collapsing, has no effect either.
- **No mixed content.** An element is parsed as either plain text or a list of child elements, never an interleaving of both — text appearing between child elements is discarded rather than bound to any member.
- **No duplicate-attribute detection.** XML 1.0 forbids two attributes with the same name on one element; XmlSerDe doesn't check for this and silently takes the first match.
- **`encoding` / `standalone` in the XML declaration are ignored** on both serialize and deserialize. XmlSerDe operates on an already-decoded `ReadOnlySpan<char>`, not raw bytes, so byte-level decoding happens before the library sees the input — a mismatch between a document's declared `encoding` and how the caller actually decoded it is not detected.

## How the generator works

`XmlDeserializeGenerator` (`IIncrementalGenerator`) triggers on any `partial class` decorated with `[XmlSubject]` or `[XmlDerivedSubject]`.

For each serializer class it emits:

| Generated file | Contents |
|----------------|----------|
| `{ClassName}.g.cs` | `Serialize` / `Deserialize` for each registered type, per exhauster/injector |
| `BuiltinCodeHelper.MainPart.g.cs` | `CutXmlHead`, shared utilities |
| `BuiltinCodeHelper.Serialization.Shared.g.cs` | XSD type name constants |
| `BuiltinCodeHelper.{ExhausterName}.g.cs` | Per-exhauster builtin serialization |
| `BuiltinCodeHelper.{InjectorName}.g.cs` | Per-injector builtin deserialization dispatch |

**Serialize:** wraps objects in `<TypeName>` elements; polymorphic types get `xmlns:xsi` + `xsi:type`; builtin members use property wrapper + XSD inner element.

**Deserialize:** walks child nodes, dispatches on element name (`SequenceEqual` on spans), resolves polymorphism via `xsi:type`, constructs objects with `new T()` or the `XmlFactory` expression.

## Performance

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

Benchmark results (.NET 7.0, Windows 11, Intel Core i7-13700H):

```
|                         Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|        'Serialize: System.Xml' | 3.676 us | 0.0351 us | 0.0293 us | 1.0300 | 0.0343 |   12960 B |
|          'Serialize: XmlSerDe' | 1.278 us | 0.0185 us | 0.0173 us | 0.5512 |      - |    6920 B |
|    'Serialize: XmlSerDe (est)' | 1.207 us | 0.0095 us | 0.0084 us | 0.3910 | 0.0019 |    4928 B |
| 'Serialize: XmlSerDe (stream)' | 1.289 us | 0.0026 us | 0.0022 us | 0.0458 |      - |     592 B |

|                         Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|      'Deserialize: System.Xml' | 7.265 us | 0.0404 us | 0.0359 us | 1.2741 | 0.0610 |   16072 B |
|        'Deserialize: XmlSerDe' | 5.437 us | 0.0486 us | 0.0455 us | 0.0610 |      - |     824 B |
```

Notes on the benchmark variants:

1. **`(est)`** — runs a length-estimation pass first, then serializes into a pre-sized `StringBuilder`. Slightly different timing, fewer allocations.
2. **`(stream)`** — serializes to UTF-8 binary via `Utf8BinaryExhauster` (discards output in the benchmark). Low allocations thanks to `ArrayPool<byte>.Rent`.
3. **Deserialize** — XmlSerDe is faster and allocates roughly 5% of the memory compared to `System.Xml.Serialization`.

### Later deserialize run (.NET 8.0)

The deserialize benchmark now marks `System.Xml` as the BenchmarkDotNet baseline, so the table also reports `Ratio` and `Alloc Ratio`. Prefer those over the absolute `Mean`: the numbers below were measured on a different runtime, SDK and OS build than the run above, and `System.Xml` — code neither project controls — moved from 7.265 us to 9.185 us between them. Comparing absolute microseconds across runs mostly measures the machine, not the library.

```
| Method                    | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------- |---------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| 'Deserialize: System.Xml' | 9.185 us | 0.1770 us | 0.2108 us |  1.00 |    0.00 | 1.3428 | 0.0610 |  16.49 KB |        1.00 |
| 'Deserialize: XmlSerDe'   | 8.165 us | 0.0490 us | 0.0459 us |  0.88 |    0.02 | 0.0763 |      - |   1.12 KB |        0.07 |
```

The XML 1.0 spec-compliance work (quote-aware tag-head scanning, the full `S` production for whitespace, attribute-value normalization) initially cost more than intended, because two of its search primitives left the vectorized path:

- `IndexOfAny('/', '>', ' ')` became `IndexOfAny("/> \t\r\n".AsSpan())`. The runtime only has SIMD implementations of `IndexOfAny(ReadOnlySpan<T>)` for **up to five** values and falls back to a probabilistic (bloom-filter) scan beyond that — and `"/> \t\r\n"` is exactly six characters.
- `IndexOf('>')` became a character-by-character quote-aware loop.

Both run for every element in the document. They were restored to vectorized equivalents that keep the spec-compliant behavior: the six-character search is split into two SIMD three-character searches (the rarer tab/CR/LF one bounded to the shorter prefix), and the quote-aware scan now jumps between boundaries via `IndexOfAny('>', '"', '\'')` plus `IndexOf(quote)`, so its iteration count tracks the number of attributes rather than the length of the tag head.

`Alloc Ratio` of 0.07 reflects the enum and `Guid` fixes described above.

### Previous deserialize run (.NET 8.0) — the quadratic-depth measurement

Two document shapes are benchmarked, each with its own `System.Xml` baseline:

- **REGULAR** — the document shown above: 26 elements, maximum nesting depth 6, indented.
- **DEEP** — one element inside another, 100 levels down, a single string at the bottom, no indentation (`DeepFixture` in `XmlSerDe.Tests/Deep`). Same work, different shape: wide-and-shallow becomes narrow-and-deep.

Adding DEEP is what exposed the problem the current run fixes:

```
| Method                             | Categories | Mean       | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------------------------- |----------- |-----------:|----------:|------:|----------:|------------:|
| 'Deserialize: DEEP: System.Xml'    | DEEP       |  15.232 us | 0.1756 us |  1.00 |  29.18 KB |        1.00 |
| 'Deserialize: DEEP: XmlSerDe'      | DEEP       | 142.771 us | 2.3115 us |  9.39 |   3.20 KB |        0.11 |
| 'Deserialize: REGULAR: System.Xml' | REGULAR    |   8.483 us | 0.2310 us |  1.00 |  16.49 KB |        1.00 |
| 'Deserialize: REGULAR: XmlSerDe'   | REGULAR    |   6.284 us | 0.1071 us |  0.74 |   1.12 KB |        0.07 |
```

Deserialization was quadratic in nesting depth: 26% faster than `System.Xml` at depth 6, **9.4× slower** at depth 100. The cause was structural. To hand back a node, `XmlNode2.GetFirst` first had to know where that node ended, and it found out by recursively parsing the node's entire subtree and discarding the result. The subtree of a node at depth *d* was therefore re-walked once per ancestor, so scanning work grew as *O(size × depth)* — roughly 2.5 million character visits for a 1550-character document nested 100 deep. Depth 6 pays only a ~5× multiplier, small enough to hide behind the allocation win; DEEP made it unmissable.

Ratio 0.88 → 0.74 (REGULAR) came from two earlier changes, found by A/B-measuring each candidate in its own BenchmarkDotNet job rather than by reasoning about complexity:

- **The tag head is scanned once, not twice.** `GetFirstLength` already determined where a node's head ends, its name, and whether it is bodyless — then threw that away and let the `XmlNode2` constructor rediscover all of it. Worth more than everything else at the time, because the scans it eliminated were the expensive ones (heads of materialized nodes).
- **Finding the end of the name and the end of the head is one pass.** A node whose name runs straight into `>` has no attributes, therefore no quotes, so the quote-aware search is skipped entirely; otherwise it starts at the end of the name instead of at zero. The tab/CR/LF check over the short prefix is scalar — those characters are all below `' '`, a legal name character is always above it, and a literal space cannot appear in the prefix by construction, so the test is equivalent to a second vectorized search but cheaper than setting one up.

### Current deserialize run (.NET 8.0) — single-pass deserialization

```
| Method                             | Categories | Mean      | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------------------------- |----------- |----------:|----------:|------:|----------:|------------:|
| 'Deserialize: DEEP: System.Xml'    | DEEP       | 16.004 us | 0.2968 us |  1.00 |  29.18 KB |        1.00 |
| 'Deserialize: DEEP: XmlSerDe'      | DEEP       |  2.839 us | 0.0270 us |  0.18 |   3.20 KB |        0.11 |
| 'Deserialize: REGULAR: System.Xml' | REGULAR    |  8.757 us | 0.1307 us |  1.00 |  16.49 KB |        1.00 |
| 'Deserialize: REGULAR: XmlSerDe'   | REGULAR    |  2.654 us | 0.0380 us |  0.30 |   1.12 KB |        0.07 |
```

| Category | Ratio before | Ratio after | Mean before | Mean after |
|----------|-------------:|------------:|------------:|-----------:|
| DEEP     | 9.39         | **0.18**    | 142.771 us  | 2.839 us   |
| REGULAR  | 0.74         | **0.30**    | 6.284 us    | 2.654 us   |

Allocations are byte-for-byte unchanged (3.20 KB and 1.12 KB) — this was purely CPU.

**The fix was to stop measuring nodes before parsing them.** A node's length was only ever needed for one thing, and only *after* the node had been parsed: advancing the cursor to the next sibling. Meanwhile the parse itself already reaches the closing tag — a child loop stops precisely at `</Child>` — so the length was being computed twice, once by a throwaway pre-walk and once by the parse that discarded it.

Generated `DeserializeBody` methods now report how much input they consumed, and the pre-walk is gone:

- `XmlScan.ReadHead` reads exactly one tag head at the cursor and never descends into the subtree, so each element's head is scanned once instead of once per ancestor.
- The body span is no longer clipped on the right; a body method runs until it meets its own end tag, which it verifies by name. That check is now the only thing bounding a nested parse, so mismatched and truncated documents are rejected there (`SinglePassParserFixture`).
- `XmlScan.SkipBody` — a cheap quote-aware tag-balance count — handles elements with no matching member. Previously *every* element was fully parsed to find its end; now this runs only for elements the POCO doesn't bind, and not at all on a document that matches its POCO.
- `XmlNode2` remains as the public node-oriented API (`DefaultInjector`, `SpecComplianceFixture`) but is no longer on the deserialization hot path.

Two behaviours changed as a side effect, both strictly less lossy than before:

- **A self-closing child no longer ends the sibling loop.** `GetFirstLength` returned length 0 for a bodyless node, which the generated loop read as "no more children" — everything after `<Foo/>` was silently dropped. Now only `<Foo/>` itself binds nothing.
- **A polymorphic member is dispatched by member name first, then by `xsi:type`.** The old codegen emitted a variable declaration between `if` and `else if`, so a polymorphic member that was not the first member produced code that did not compile; it also matched any child carrying an `xsi:type` regardless of the member's name.

Instrumentation counted **110** head scans per deserialize of a **26**-element document before this change, of which 78 existed only to skip over subtrees, and total character traffic was 3.4× the document length. The analysis, the counters, and the design are in [docs/perf-single-pass-parser.md](docs/perf-single-pass-parser.md); the earlier investigation that first identified the multiplier is in [docs/perf-redundant-head-scans.md](docs/perf-redundant-head-scans.md). Both also document the benchmarking methodology — including why an A/B switch must be a `static readonly` field read from an environment variable (a plain mutable `static bool` breaks inlining and distorted an entire run by ~1 us).

Example serializer declaration and usage from the benchmark fixture:

```csharp
public InfoContainer Deserialize(ReadOnlySpan<char> xml)
{
    XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer r);
    return r;
}

public string Serialize()
{
    var dsbe = new DefaultStringBuilderExhauster();
    XmlSerializerDeserializer.Serialize(dsbe, DefaultObject, false);
    return dsbe.ToString();
}

public string Serialize_Est()
{
    var dlee = new DefaultLengthEstimatorExhauster();
    XmlSerializerDeserializer.Serialize(dlee, DefaultObject, false);
    var estimateXmlLength = dlee.EstimatedTotalLength;

    var dsbe = new DefaultStringBuilderExhauster(new StringBuilder(estimateXmlLength));
    XmlSerializerDeserializer.Serialize(dsbe, DefaultObject, false);
    return dsbe.ToString();
}

public void Serialize_ToStream_Test()
{
    var be = new Utf8BinaryExhausterEmpty();
    XmlSerializerDeserializer.Serialize(be, DefaultObject, false);
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

Run benchmarks:

```bash
dotnet run -c Release --project XmlSerDe.PerformanceTests
```

`Program.Main` runs `SerializeDeserializeFixture`; the other fixtures are listed there commented out, to be swapped in as needed. Among them, `AllocationHotspotsFixture` isolates the individual allocation sources that the enum and `Guid` fixes addressed (`Enum.ToString()`, `Enum.Parse` boxing, `StringBuilder.Append(object?)` boxing of `Guid`), measuring each on its own and in small batches so the per-call cost is visible in `Allocated` rather than lost in the noise of a full document parse.

## Building and testing

```bash
dotnet build XmlSerDe.sln
dotnet test XmlSerDe.Tests
```

Generated source files are written to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled (as in the test project).

## Test coverage map

`XmlSerDe.Tests/SerDeFixture.cs` exercises the main features:

| Tests | Feature |
|-------|---------|
| `XmlObject1_*` | Empty root, self-closing tags, `xsi:type`, XML comments, stream serialization |
| `XmlObject2_*` | Primitives, HTML entity decoding, XML declaration stripping |
| `XmlObject4_5_*` | Abstract polymorphism |
| `XmlObject6_*` | `List<string>` |
| `XmlObject7_8_*` | Public fields |
| `XmlObject9_10_*`, `XmlObject11_12_*` | Non-abstract / abstract base polymorphism |
| `XmlObject13_*` | `[XmlIgnore]` |
| `XmlObject14_*` | Nullable `DateTime?`, `Guid?` |
| `XmlObject15_*` | Enums |
| `XmlObject16_17_*` | `List<CustomType>` |
| `XmlObject18_*` – `XmlObject22_*` | Enum and primitive arrays |
| `XmlObject23_24_*` | Arrays of custom types |
| `XmlObject25_26_27_*`, `XmlObject28_29_30_*` | Polymorphism on nested properties and in lists |
| `ComplexFixture` / `ComplexFixtureV2` | Full document with derived types, enums, `DateTime`, `XmlFactory` reuse |
| `DeepFixture` | Self-referencing type nested 100 levels deep; guards the DEEP benchmark by asserting the whole chain is walked and matches `System.Xml` |
| `SinglePassParserFixture` | Consequences of single-pass deserialization: unknown elements skipped by tag balance (incl. `>` inside attribute values, CDATA, nested children), self-closing children not ending the sibling loop, mismatched/truncated closing tags rejected, compact vs. indented parity |
| `SerDeFixtureV2` | Same feature set as `SerDeFixture`, exercised through a second serializer declaration to catch cross-class code-gen issues |
| `CoverageExpansionFixture` | All primitive types incl. `decimal`/`Guid` round-trips, nullable value-type omission on serialize, length-estimator accuracy, empty/null collections, CDATA strings (including concatenated blocks), HTML-entity-encoded string serialization |
| `SpecComplianceFixture` | XML 1.0 edge cases: unescaped `>` in attribute values (including a foreign-producer-style extra attribute during polymorphic deserialize), prolog processing instructions / `DOCTYPE` (incl. internal subset) being skipped, attribute-value whitespace normalization vs. character references |

## Alternatives

You may also be interested in [StackXML](https://github.com/ZingBallyhoo/StackXML).
