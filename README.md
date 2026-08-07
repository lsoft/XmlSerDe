# XmlSerDe

Allocation-free XML serializer/deserializer based on C# incremental source generators (ISG). Because generation happens at compile time, there is no runtime reflection cost and no performance degradation as the number of serialized types grows.

XmlSerDe's purpose is **POCO ↔ XML data binding**: mapping plain C# classes to XML and back, format-compatible with `System.Xml.Serialization` for the primitives, collections, and `xsi:type` polymorphism it supports. It targets that data-binding scenario specifically rather than being a general-purpose XML processor — see [Out of scope by design](#out-of-scope-by-design) for what that excludes and why.

## Status

The API and behavior may still change between releases.

## Performance

Serialization code is generated at compile time, so there is no runtime reflection, no first-call warm-up, and no degradation as the number of serialized types grows. Deserialization reads the document as a `ReadOnlySpan<char>` and never materializes an intermediate node tree. Measured against `System.Xml.Serialization` on the same documents and the same machine, that buys four things:

- **Deserialization allocates the resulting object graph and nothing else** — 808 B against 16 898 B on the REGULAR document, under 5% of the baseline, and none of it survives to gen1 while every `System.Xml` run promotes something. Entity references and CDATA are expanded straight out of the span; array members are accumulated through `ArrayPool<T>` rather than a throwaway `List<T>`.
- **Deserialization is single-pass.** Each element's tag head is scanned exactly once, so cost is linear in document size and independent of nesting depth: **3.8×** faster than `System.Xml` on a shallow document, **5.7×** on one nested 100 levels deep.
- **Serialization offers three modes with different allocation profiles**, down to **384 B** — 3% of the baseline — when writing UTF-8 straight to a stream, at the same speed as the other two.
- **The hot paths are multi-targeted.** `SearchValues<char>`, span `TryFormat` and span `Parse` overloads are used on net8.0+; the netstandard2.0 fallbacks are still 1.4–1.9× faster than `System.Xml` on the runtime that consumes them.

### What is measured

Two document shapes, each with its own `System.Xml` baseline:

- **REGULAR** — 26 elements, maximum nesting depth 6, indented, with derived types dispatched by `xsi:type`, an enum, a `DateTime`, entity-encoded text and CDATA. This is `ComplexFixture.AuxXml`, reproduced [at the end of this section](#the-regular-document).
- **DEEP** — one element inside another, 100 levels down, a single string at the bottom, no indentation (`DeepFixture` in `XmlSerDe.Tests/Deep`). The same work in a different shape: wide-and-shallow becomes narrow-and-deep, which is what makes any per-ancestor re-walking visible.

BenchmarkDotNet v0.15.2, Windows 11, 13th Gen Intel Core i7-13700H, .NET SDK 10.0.302. Reproduce with `run-benchmarks.bat`.

### Deserialization

Run under all three target frameworks. `.NET Framework 4.7.2` is not there for .NET Framework's own sake — it is the only way to *execute* the netstandard2.0 assemblies, so that row is the `#else` branches being measured.

```
| Method                             | Runtime              | Categories | Mean      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: DEEP: System.Xml'    | .NET 10.0            | DEEP       | 15.922 us |  1.00 | 2.3193 | 0.1221 |   29824 B |        1.00 |
| 'Deserialize: DEEP: XmlSerDe'      | .NET 10.0            | DEEP       |  2.817 us |  0.18 | 0.2594 |      - |    3272 B |        0.11 |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: DEEP: System.Xml'    | .NET 8.0             | DEEP       | 16.971 us |  1.00 | 2.3193 | 0.1221 |   29880 B |        1.00 |
| 'Deserialize: DEEP: XmlSerDe'      | .NET 8.0             | DEEP       |  2.703 us |  0.16 | 0.2594 |      - |    3272 B |        0.11 |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: DEEP: System.Xml'    | .NET Framework 4.7.2 | DEEP       | 21.761 us |  1.00 | 5.2795 | 0.4578 |   33250 B |        1.00 |
| 'Deserialize: DEEP: XmlSerDe'      | .NET Framework 4.7.2 | DEEP       | 11.460 us |  0.53 | 0.5188 |      - |    3290 B |        0.10 |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: REGULAR: System.Xml' | .NET 10.0            | REGULAR    |  7.951 us |  1.00 | 1.3428 | 0.0610 |   16898 B |        1.00 |
| 'Deserialize: REGULAR: XmlSerDe'   | .NET 10.0            | REGULAR    |  2.071 us |  0.26 | 0.0610 |      - |     808 B |        0.05 |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: REGULAR: System.Xml' | .NET 8.0             | REGULAR    |  8.620 us |  1.00 | 1.3428 | 0.0610 |   16888 B |        1.00 |
| 'Deserialize: REGULAR: XmlSerDe'   | .NET 8.0             | REGULAR    |  2.510 us |  0.29 | 0.0610 |      - |     808 B |        0.05 |
|----------------------------------- |--------------------- |----------- |----------:|------:|-------:|-------:|----------:|------------:|
| 'Deserialize: REGULAR: System.Xml' | .NET Framework 4.7.2 | REGULAR    | 11.306 us |  1.00 | 2.7161 | 0.1221 |   17114 B |        1.00 |
| 'Deserialize: REGULAR: XmlSerDe'   | .NET Framework 4.7.2 | REGULAR    |  8.218 us |  0.73 | 0.1984 |      - |    1292 B |        0.08 |
```

`Gen0` and `Gen1` are collections per 1000 operations, and they are the part of the memory story `Allocated` cannot tell. XmlSerDe triggers no gen1 collection in any of the six pairs; `System.Xml` triggers one in all six, because its intermediate reader state outlives the gen0 collection that its own allocation rate provokes. Nothing XmlSerDe allocates survives long enough to be promoted — what is left is the object graph, and the caller is still holding that. The gen0 rate falls 22× on REGULAR under .NET 10 and 10× on DEEP under .NET Framework, where the baseline is heaviest.

### Serialization

Measured on net10.0 only: serialization touches neither `XmlScan` nor `XmlTextDecoder`, so it says nothing about the difference between the netstandard2.0 and net8.0+ branches.

```
| Method                         | Mean     | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------- |---------:|------:|-------:|-------:|----------:|------------:|
| 'Serialize: System.Xml'        | 3.751 us |  1.00 | 1.0681 | 0.0381 |   13456 B |        1.00 |
| 'Serialize: XmlSerDe'          | 1.134 us |  0.30 | 0.5512 | 0.0057 |    6928 B |        0.51 |
| 'Serialize: XmlSerDe (est)'    | 1.047 us |  0.28 | 0.4025 | 0.0019 |    5072 B |        0.38 |
| 'Serialize: XmlSerDe (stream)' | 1.140 us |  0.30 | 0.0305 |      - |     384 B |        0.03 |
```

The three XmlSerDe rows are three exhausters, not three implementations:

1. **plain** — appends into a `StringBuilder` that grows as needed.
2. **`(est)`** — runs a length-estimation pass first, then serializes into a pre-sized `StringBuilder`. One extra walk of the object graph buys the removal of every intermediate buffer: same time, 27% fewer bytes.
3. **`(stream)`** — writes UTF-8 binary through `Utf8BinaryExhauster` into an `ArrayPool<byte>` buffer (the benchmark discards the output). No string is ever built, which is where the 384 B comes from.

### How to read these tables

- **Read `Ratio`, not `Mean`.** Every absolute figure here is specific to one machine, one SDK and one OS build. `System.Xml` is code neither project controls, and across earlier runs of this same benchmark its REGULAR baseline has moved between 7.3 and 9.2 us. Comparing microseconds across runs mostly measures the machine; comparing a benchmark to the baseline captured beside it does not.
- **Each runtime is its own comparison.** .NET Framework's `System.Xml` is ~40% slower than .NET 10's to begin with, so its rows must be read against its own 1.00 and never against .NET 10's. Doing that, XmlSerDe wins by 1.9× on DEEP and 1.4× on REGULAR there, versus 5.7× and 3.8× on .NET 10.
- **`Alloc Ratio` is the more stable of the two.** Allocation is deterministic — it does not drift with CPU frequency, background load or JIT tiering — so 0.05 on REGULAR is a firmer claim than any timing on this page.
- **DEEP is a stress shape, not a realistic document.** It exists to make an *O(size × depth)* algorithm impossible to miss; see below.

### What the numbers mean

**Deserialization allocates the object graph, and only the object graph.** 808 B on REGULAR is exactly the resulting objects — the same property DEEP has, where all 3272 B are the 100 nodes plus their payload string. Two separate sources of waste were removed to get there, both found by decomposing an allocation figure rather than by reading code (`GC.GetAllocatedBytesForCurrentThread` deltas around a single warmed-up call, which agree with BenchmarkDotNet to the byte):

| | Before | After |
|---|---:|---:|
| A member with entity references | 232 B | **104 B** |
| A member with two CDATA sections | 384 B | **176 B** |
| REGULAR document, whole deserialize | 1144 B | **808 B** |

`WebUtility.HtmlDecode` takes a `string`, so every text body containing a reference had to be materialized just to be handed over and thrown away — 336 of 1144 bytes on REGULAR, 29%, that never reached the result. `XmlTextDecoder` expands references and CDATA straight out of the span into a buffer (stack below 256 chars, `ArrayPool` above) and materializes exactly once.

Array members were accumulated in a `List<T>` and copied out with `ToArray()`, costing the list object, its backing array, another array per doubling, and finally the result. `PooledArrayBuilder<T>` takes the intermediate buffers from `ArrayPool<T>`:

| Member | Before | After | The result itself |
|---|---:|---:|---:|
| `int[1]` | 128 B | **56 B** | 32 B + 24 B for the POCO |
| `int[10]` | 304 B | **88 B** | 64 B + 24 B |
| `int[100]` | 1632 B | **448 B** | 424 B + 24 B |
| `int[1000]` | 12472 B | **4048 B** | 4024 B + 24 B |

The "after" column is exactly the array plus the object holding it: the overhead is not reduced but gone. Neither benchmark document has an array member, so this does not appear in the tables above — it was measured directly.

**DEEP is in the suite because it once read 9.39.** Deserialization used to be quadratic in nesting depth: 26% faster than `System.Xml` at depth 6, **9.4× slower** at depth 100. To hand back a node, `XmlNode2.GetFirst` first had to know where that node ended, and it found out by recursively parsing the node's entire subtree and discarding the result — so the subtree of a node at depth *d* was re-walked once per ancestor. Instrumentation counted 110 head scans per deserialize of a 26-element document, of which 78 existed only to skip over subtrees, with total character traffic 3.4× the document length.

| Category | Ratio before | Ratio now |
|----------|-------------:|----------:|
| DEEP     | 9.39         | **0.18**  |
| REGULAR  | 0.74         | **0.26**  |

The fix was to stop measuring nodes before parsing them: generated `DeserializeBody` methods now report how much input they consumed, `XmlScan.ReadHead` reads one tag head and never descends, and an unbound element is skipped by a cheap quote-aware tag-balance count instead of being fully parsed. `XmlNode2` remains as the public node-oriented API but is off the hot path. The analysis, the counters and the design are in [docs/perf-single-pass-parser.md](docs/perf-single-pass-parser.md); the earlier investigation that first identified the multiplier is in [docs/perf-redundant-head-scans.md](docs/perf-redundant-head-scans.md). Both also document the benchmarking methodology — including why an A/B switch must be a `static readonly` field read from an environment variable (a plain mutable `static bool` breaks inlining and distorted an entire run by ~1 us).

Two behaviours changed as a side effect, both strictly less lossy than before: a self-closing child no longer ends the sibling loop (`GetFirstLength` returned length 0 for a bodyless node, which the generated loop read as "no more children", silently dropping everything after `<Foo/>`), and a polymorphic member is now dispatched by member name first and `xsi:type` second.

**Spec compliance is not paid for in the hot loop.** Quote-aware tag-head scanning, the full `S` production for whitespace, and attribute-value normalization initially cost more than intended, because `IndexOfAny("/> \t\r\n")` is six characters and the runtime only vectorizes `IndexOfAny(ReadOnlySpan<T>)` for up to five, falling back to a probabilistic scan beyond that. On net8.0+ that search is now a `SearchValues<char>`, which builds an ASCII bitmap once per process and has no such limit. The netstandard2.0 branch keeps the original workaround: one vectorized `IndexOfAny('/', '>', ' ')` plus a scalar sweep of the short prefix for tab/CR/LF — those are all below `' '` and a legal name character is always above it, so the test is equivalent to a second vectorized search but cheaper than setting one up.

**The netstandard2.0 gap is visible and explainable.** REGULAR allocates 1292 B there against 808 B elsewhere: net8.0+ passes the `ReadOnlySpan<char>` straight into `int.Parse`/`DateTime.Parse`, while netstandard2.0 has no span overloads and must materialize a string per parsed value. DEEP does no parsing at all — one string member at the bottom of the chain — so it stays essentially flat, 3290 B against 3272 B.

**net10.0 and net8.0 are within noise of each other**, in both directions across the two categories. That is the expected result rather than a surprise: every fast path is gated on `NET8_0_OR_GREATER` and nothing is gated on net9 or net10, so the two builds compile the same source. The third target is there to keep that claim honest, and to be where a net9/net10-only API — say `SearchValues.Create(ReadOnlySpan<string>)` for entity names — would land if one were added.

### Running the benchmarks

```bash
run-benchmarks.bat
```

The batch file builds in `Release` and prints only the result tables; `dotnet build` output and BenchmarkDotNet's own progress log go to `benchmarks.log`.

`Program.Main` runs two fixtures. They are split because BenchmarkDotNet assigns a job to a whole class — a single `[Benchmark]` cannot pick its own runtime:

| Fixture | Runtimes | What it covers |
|---------|----------|----------------|
| `SerializeFixture` | net10.0 | `Serialize:` — plain, length-estimated, and stream variants |
| `DeserializeFixture` | net472, net8.0, net10.0 | `Deserialize:` — `DEEP` and `REGULAR` categories |

The other fixtures are listed in `Program.Main` commented out, to be swapped in as needed. Among them, `AllocationHotspotsFixture` isolates the individual allocation sources that the enum and `Guid` fixes addressed (`Enum.ToString()`, `Enum.Parse` boxing, `StringBuilder.Append(object?)` boxing of `Guid`), measuring each on its own and in small batches so the per-call cost shows up in `Allocated` rather than being lost in the noise of a full document parse. It and `XmlDecodeStringFixture` measure APIs that do not exist on netstandard2.0 (`Enum.Parse(Type, ReadOnlySpan<char>)`, `Encoding.GetBytes(string, Span<byte>)`), so they are excluded from the `net472` compile rather than rewritten into measuring something else.

#### The REGULAR document

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

The serializer declaration it is bound to, and the four benchmarked entry points:

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
[XmlFactory(typeof(InfoContainer), "global::" + "XmlSerDe.Tests.Complex.Subject" + "." + nameof(CachedInfoContainer) + "." + nameof(CachedInfoContainer.Reuse) + "()")]
public partial class XmlSerializerDeserializer
{
}
```

## Solution structure

| Project | Target | Role |
|---------|--------|------|
| **XmlSerDe.Common** | netstandard2.0; net8.0; net10.0 | Attributes, `IInjector` / `IExhauster` contracts, `XmlScan`/`XmlTextDecoder`, and `XmlNode2` — a low-allocation XML node parser over `ReadOnlySpan<char>`. |
| **XmlSerDe.Components** | netstandard2.0; net8.0; net10.0 | Default runtime implementations: injectors and exhausters. |
| **XmlSerDe.Generator** | netstandard2.0 (Roslyn analyzer) | Incremental source generator that emits serialization/deserialization code at compile time. |
| **XmlSerDe.Tests** | net472; net8.0; net10.0 | Functional tests (xUnit). |
| **XmlSerDe.PerformanceTests** | net472; net8.0; net10.0 | BenchmarkDotNet benchmarks vs `System.Xml.Serialization`. |

The two runtime libraries are multi-targeted so that a consumer on a modern runtime gets the fast paths — `SearchValues<char>`, span `TryFormat`, span `Parse` overloads — while a netstandard2.0 consumer still compiles and runs. Only method bodies differ between targets; the public surface is identical, because the generator itself is netstandard2.0 and compiles against that build. `net472` in the test and benchmark projects is not about .NET Framework: netstandard2.0 cannot be executed directly, and `net472` is what consumes those assets, so it is the only target under which the `#else` branches actually run. The cost of those fallbacks is measured, not assumed — see [What the numbers mean](#what-the-numbers-mean).

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

### `[XmlInclude(typeof(Derived))]` — `System.Xml.Serialization`

Enables polymorphic serialization and deserialization via `xsi:type`. Goes on the **base type**, not on the serializer class, and is the very same attribute `System.Xml.Serialization` reads — so a type already annotated for the BCL needs nothing added.

The generator registers each included type as a subject on its own, so a derived type needs no `[XmlSubject]` of its own. Includes are followed recursively: a derived type may declare its own. Abstract bases require at least one include.

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
[XmlInclude(typeof(Derived1Info))]
public abstract class BaseInfo { /* ... */ }

[XmlSubject(typeof(BaseInfo), false)]
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
- **Inheritance** — via `[XmlInclude]` and `xsi:type`
- **Collections** — `List<T>` and `T[]` only

### Members

- Public fields and properties (including inherited) with accessible setters
- `[XmlIgnore]` properties are skipped
- Private and protected members are skipped
- XML names default to the C# type and member names, and are overridden by the `System.Xml.Serialization` naming attributes: `[XmlRoot]`, `[XmlType]`, `[XmlElement]`, `[XmlArray]`, `[XmlArrayItem]`, `[XmlEnum]`. `[XmlElement(Order = n)]` / `[XmlArray(Order = n)]` set the element order on write.

## Limitations

- **No CDATA serialization** — string content is always emitted entity-escaped, never wrapped in `<![CDATA[...]]>`. Deserialization does read CDATA sections, in any position within an element's text and any number of them.
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

- **No DTD support.** A `<!DOCTYPE ...>` in the prolog is skipped over, not parsed — so any custom general entities it declares are not resolved. Only the five predefined XML entities (`&amp;`, `&lt;`, `&gt;`, `&apos;`, `&quot;`) plus character references (`&#49;`, `&#x31;`) are understood; a reference to anything else — including an HTML named entity such as `&nbsp;` — throws, because without a DTD declaring it that is a well-formedness error (XML 1.0 §4.1, WFC: Entity Declared) rather than text to pass through. There's also no DTD-based content validation and no fetching of external DTDs.
- **No general XML Namespaces support.** Only one namespace is special-cased: `xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"` for `xsi:type` polymorphism (the prefix itself is flexible — deserialize resolves whichever prefix is actually bound to that URI). Beyond that, element and attribute names are compared as literal text, prefix included; there's no general prefix-to-URI resolution or default-namespace (`xmlns="uri"`) handling.
- **No XML-attribute data binding.** Every serialized member becomes a child element; there's no equivalent of `System.Xml.Serialization`'s `[XmlAttribute]` or `[XmlText]`. (`[XmlIgnore]` is the exception — XmlSerDe recognizes `System.Xml.Serialization.XmlIgnoreAttribute` directly, so it can be reused as-is.)
- **`xml:space`, `xml:lang`, `xml:base` are not interpreted.** In practice this rarely matters for `xml:space`: text content is always preserved verbatim regardless (matching the XML default, `xml:space="preserve"`) — but `xml:space="default"`, which would opt back into whitespace collapsing, has no effect either.
- **No mixed content.** An element is parsed as either plain text or a list of child elements, never an interleaving of both — text appearing between child elements is discarded rather than bound to any member.
- **No duplicate-attribute detection.** XML 1.0 forbids two attributes with the same name on one element; XmlSerDe doesn't check for this and silently takes the first match.
- **`encoding` / `standalone` in the XML declaration are ignored** on both serialize and deserialize. XmlSerDe operates on an already-decoded `ReadOnlySpan<char>`, not raw bytes, so byte-level decoding happens before the library sees the input — a mismatch between a document's declared `encoding` and how the caller actually decoded it is not detected.

## How the generator works

`XmlDeserializeGenerator` (`IIncrementalGenerator`) triggers on any `partial class` decorated with `[XmlSubject]`.

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

## Building and testing

```bash
dotnet build XmlSerDe.sln
dotnet test XmlSerDe.Tests
```

`dotnet test` runs the whole suite three times, once per target framework, so the netstandard2.0 code paths are executed rather than merely compiled.

Generated source files are written to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled (as in the test project).

## Test coverage map

`XmlSerDe.Tests/SerDeFixture.cs` exercises the main features:

| Tests | Feature |
|-------|---------|
| `XmlObject1_*` | Empty root, self-closing tags, `xsi:type`, XML comments, stream serialization |
| `XmlObject2_*` | Primitives, XML entity decoding, XML declaration stripping |
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
| `XmlTextDecoderFixture` | Reference expansion per XML 1.0 §4.1: the five predefined entities, decimal/hex character references incl. above-BMP surrogate pairs, CDATA in any position, attribute-value normalization vs. references, and every reference form XML rejects (undeclared, unterminated, empty, uppercase `X`, illegal or out-of-range code point) |
| `PooledArrayBuilderFixture` | Array members across the pooled builder's growth steps (every other array test uses 3 elements and never reaches one), for primitive, struct and reference element types; empty-array and builder-reuse semantics |
| `SerDeFixtureV2` | Same feature set as `SerDeFixture`, exercised through a second serializer declaration to catch cross-class code-gen issues |
| `CoverageExpansionFixture` | All primitive types incl. `decimal`/`Guid` round-trips, nullable value-type omission on serialize, length-estimator accuracy, empty/null collections, CDATA strings (including concatenated blocks), HTML-entity-encoded string serialization |
| `SpecComplianceFixture` | XML 1.0 edge cases: unescaped `>` in attribute values (including a foreign-producer-style extra attribute during polymorphic deserialize), prolog processing instructions / `DOCTYPE` (incl. internal subset) being skipped, attribute-value whitespace normalization vs. character references |

## Alternatives

You may also be interested in [StackXML](https://github.com/ZingBallyhoo/StackXML).
