# XmlSerDe

Allocation-free XML serializer/deserializer based on C# incremental source generators (ISG). Because generation happens at compile time, there is no runtime reflection cost and no performance degradation as the number of serialized types grows.

XmlSerDe's purpose is **POCO ↔ XML data binding**: mapping plain C# classes to XML and back, format-compatible with `System.Xml.Serialization` for the primitives, collections, and `xsi:type` polymorphism it supports. It targets that data-binding scenario specifically rather than being a general-purpose XML processor — see [Out of scope by design](#out-of-scope-by-design) for what that excludes and why. Existing `XmlSerializer` code can be moved over one project at a time without being rewritten — see [Drop-in mode](#drop-in-mode-xmlserdecompat), and note what it deliberately does not accelerate.

## Status

The API and behavior may still change between releases.

**Breaking change (native path):** a `[XmlSubject]` serializer without `[XmlFeatures]` no longer reads comments, CDATA, processing instructions, DOCTYPE, single-quoted attributes, `>` inside attribute values, or a non-`xsi` prefix for `xsi:type` / `xsi:nil`, and string serialization no longer runs `XmlCharGuard`. That is the fast POCO path. To get the previous native behaviour:

```csharp
[XmlFeatures(XmlFeature.SystemXmlCompatible)]
[XmlSubject(typeof(Order), true)]
public partial class OrderSerializer { }
```

`XmlSerDe.Compat` turns those features on by itself; user code does not add the attribute. See [Opt-in XML features](#opt-in-xml-features).

Two smaller breaks come with it: a custom `IExhauster` now also implements `AppendEncodedUnchecked` / `AppendAttributeEncodedUnchecked` (the unguarded pair the default path calls), and `BuiltinCodeHelper.CutXmlHead(span)` alone no longer skips comments, PIs or a DOCTYPE — call the generated `YourSerializer.CutXmlHead(span)`, which passes that host's own flags.

`XmlFeature` itself was then cut from seven flags to four. `QuotedAttributes` is gone and its behaviour is unconditional; `Comments`, `ProcessingInstructions` and `Doctype` are gone and folded into a single `Markup`, which `CData` now includes. `SystemXmlCompatible` is unchanged in meaning. The reasoning, with numbers, is in [Cost of turning a flag on](#cost-of-turning-a-flag-on).

**Breaking change (attribute syntax, every host):** a broken attribute head is now an error instead of a wrong object. `<Foo id=1 Tag="x">` used to read as `id="x"` — the value stolen from the neighbouring attribute, `Tag` lost — and `<Foo id Tag="x">` used to produce an attribute named `"id Tag"` that matched nothing. Both now throw. This is not a flag: there is no mode in which reading the neighbour's value is correct. The same check repairs the mirror bug on **valid** input — `<Foo a = "1">` is legal XML that used to be dropped silently, because the attribute name ended only at `=`, `:`, `/` or `>`.

**Breaking change (drop-in):** `XmlSerDe.Compat` now refuses malformed documents the way `System.Xml.Serialization` refuses them — a second root element, a duplicate attribute, a mismatched or missing closing tag, an illegal `Char` in a string. Code that relied on the facade quietly accepting broken input will start seeing `InvalidOperationException` → `XmlException`, which is what the BCL raised there all along. The native path is unaffected unless you ask for it with `[XmlGuards]` — see [Opt-in XML guards](#opt-in-xml-guards).

## Performance

XmlSerDe generates serialize and deserialize methods at compile time, so there is no runtime reflection, no first-call warm-up, and no extra cost as the type graph grows. Against `System.Xml.Serialization` on the same documents, a typical shallow payload deserializes about **4× faster** and allocates **5%** of the baseline — the object graph, and nothing else. A document nested 100 levels deep is about **5× faster** for the same reason: the parser is single-pass, so depth no longer multiplies the work. A ~100 MB document is still **twice as fast** and uses **39%** of the BCL heap. Serialization to a string is about **3×** on the small document; writing UTF-8 into a pre-sized `MemoryStream` on the 100 MB document is twice as fast and allocates **28%** of the BCL stream write. On .NET Framework the win is smaller — span `Parse` / `TryFormat` are missing — and on that HUGE deserialize XmlSerDe is a few percent *slower*, though it still allocates 41% of the baseline.

<details>
<summary>Benchmark tables (click to expand)</summary>

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8875), 13th Gen Intel Core i7-13700H, .NET SDK 11.0.100-preview.6.26359.118. Host: .NET 10.0.11. Reproduce with `run-benchmarks.bat`. `Ratio` and `Alloc Ratio` are against the `System.Xml` row in the same group. `Gen0` / `Gen1` are collections per 1000 operations. Error, StdDev and RatioSD are omitted here; they are in `benchmarks.log`.

These tables were captured **before** native XML extras became opt-in and before `AuxXml` dropped CDATA / `p3:type`. They remain the last official BDN snapshot; re-run `run-benchmarks.bat` for a new default-path baseline. Relative cost of each flag on a small POCO document is in [Cost of turning a flag on](#cost-of-turning-a-flag-on).

#### Deserialization

| Method   | Categories | Runtime              | Mean      | Ratio | Gen0       | Gen1       | Allocated   | Alloc Ratio |
|----------|----------- |--------------------- |----------:|------:|-----------:|-----------:|------------:|------------:|
| System.Xml | DEEP     | .NET 10.0            |  15.107 µs |  1.00 |     2.3193 |     0.1221 |     29824 B |        1.00 |
| XmlSerDe   | DEEP     | .NET 10.0            |   3.139 µs |  0.21 |     0.2594 |          - |      3272 B |        0.11 |
| System.Xml | DEEP     | .NET 8.0             |  15.319 µs |  1.00 |     2.3804 |     0.2289 |     29880 B |        1.00 |
| XmlSerDe   | DEEP     | .NET 8.0             |   3.187 µs |  0.21 |     0.2594 |          - |      3272 B |        0.11 |
| System.Xml | DEEP     | .NET Framework 4.7.2 |  20.946 µs |  1.00 |     5.2795 |     0.4578 |     33250 B |        1.00 |
| XmlSerDe   | DEEP     | .NET Framework 4.7.2 |  11.552 µs |  0.55 |     0.5188 |          - |      3290 B |        0.10 |
| System.Xml | HUGE     | .NET 10.0            | 657.948 ms |  1.00 | 21000.0000 | 10000.0000 | 258222904 B |        1.00 |
| XmlSerDe   | HUGE     | .NET 10.0            | 297.380 ms |  0.45 |  8000.0000 |  7000.0000 | 100374616 B |        0.39 |
| System.Xml | HUGE     | .NET 8.0             | 706.800 ms |  1.00 | 21000.0000 | 11000.0000 | 259647152 B |        1.00 |
| XmlSerDe   | HUGE     | .NET 8.0             | 343.087 ms |  0.49 |  8000.0000 |  7000.0000 | 100378480 B |        0.39 |
| System.Xml | HUGE     | .NET Framework 4.7.2 |    1.020 s |  1.00 | 49000.0000 | 14000.0000 | 307378176 B |        1.00 |
| XmlSerDe   | HUGE     | .NET Framework 4.7.2 |    1.064 s |  1.04 | 21000.0000 |  8000.0000 | 127341776 B |        0.41 |
| System.Xml | REGULAR  | .NET 10.0            |   7.757 µs |  1.00 |     1.3428 |     0.0610 |     16904 B |        1.00 |
| XmlSerDe   | REGULAR  | .NET 10.0            |   2.063 µs |  0.27 |     0.0610 |          - |       792 B |        0.05 |
| System.Xml | REGULAR  | .NET 8.0             |   8.169 µs |  1.00 |     1.3428 |     0.0610 |     16888 B |        1.00 |
| XmlSerDe   | REGULAR  | .NET 8.0             |   2.633 µs |  0.32 |     0.0610 |          - |       792 B |        0.05 |
| System.Xml | REGULAR  | .NET Framework 4.7.2 |  11.146 µs |  1.00 |     2.7161 |     0.1221 |     17114 B |        1.00 |
| XmlSerDe   | REGULAR  | .NET Framework 4.7.2 |   7.954 µs |  0.71 |     0.1678 |          - |      1107 B |        0.06 |

#### Serialization

`stringbuilder` = `StringBuilderExhauster` / `StringWriter`. `pooledchar` = `PooledCharExhauster` after a `LengthEstimatorExhauster` walk. `emptystream` = `Utf8BinaryExhausterEmpty` (UTF-8 encode, no I/O). `memorystream` = buffered UTF-8 `MemoryStream`; `Preestimate` = true sets `Capacity` to 1.1× the char estimate. Memory-stream rows have their own `System.Xml` baseline (`XmlWriter` to the stream), not `StringWriter`.

**REGULAR / DEEP** (mean in µs):

| Method     | Categories | Exhauster     | Preestimate | Runtime              | Mean     | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------- |----------- |-------------- |------------ |--------------------- |---------:|------:|--------:|-------:|----------:|------------:|
| System.Xml | DEEP       | stringbuilder | false       | .NET 10.0            |  45.265 |  1.00 | 11.1694 | 2.7466 |  140152 B |       1.000 |
| XmlSerDe   | DEEP       | stringbuilder | false       | .NET 10.0            |   0.697 |  0.02 |  0.6237 | 0.0105 |    7832 B |       0.056 |
| XmlSerDe   | DEEP       | pooledchar    | true        | .NET 10.0            |   0.669 |  0.01 |  0.2537 |      - |    3192 B |       0.023 |
| XmlSerDe   | DEEP       | emptystream   | false       | .NET 10.0            |   0.939 |  0.02 |  0.0668 |      - |     840 B |       0.006 |
| System.Xml | DEEP       | stringbuilder | false       | .NET 8.0             |  54.009 |  1.00 | 11.1084 | 2.4414 |  140048 B |       1.000 |
| XmlSerDe   | DEEP       | stringbuilder | false       | .NET 8.0             |   0.694 |  0.01 |  0.6237 | 0.0105 |    7832 B |       0.056 |
| XmlSerDe   | DEEP       | pooledchar    | true        | .NET 8.0             |   1.077 |  0.02 |  0.2537 |      - |    3192 B |       0.023 |
| XmlSerDe   | DEEP       | emptystream   | false       | .NET 8.0             |   1.313 |  0.02 |  0.0668 |      - |     840 B |       0.006 |
| System.Xml | DEEP       | stringbuilder | false       | .NET Framework 4.7.2 |  64.100 |  1.00 | 22.8271 | 3.6621 |  144548 B |       1.000 |
| XmlSerDe   | DEEP       | stringbuilder | false       | .NET Framework 4.7.2 |   3.651 |  0.06 |  1.2474 | 0.0191 |    7856 B |       0.054 |
| XmlSerDe   | DEEP       | pooledchar    | true        | .NET Framework 4.7.2 |   3.256 |  0.05 |  0.5074 |      - |    3202 B |       0.022 |
| XmlSerDe   | DEEP       | emptystream   | false       | .NET Framework 4.7.2 |   3.555 |  0.06 |  0.1335 |      - |     843 B |       0.006 |
| System.Xml | DEEP       | memorystream  | false       | .NET 10.0            |  43.610 |  1.00 |  5.9814 | 0.6104 |   75760 B |        1.00 |
| XmlSerDe   | DEEP       | memorystream  | false       | .NET 10.0            |   1.528 |  0.04 |  0.1984 |      - |    2496 B |        0.03 |
| XmlSerDe   | DEEP       | memorystream  | true        | .NET 10.0            |   1.665 |  0.04 |  0.2136 |      - |    2680 B |        0.04 |
| System.Xml | DEEP       | memorystream  | false       | .NET 8.0             |  50.008 |  1.00 |  5.9814 | 0.6104 |   75760 B |        1.00 |
| XmlSerDe   | DEEP       | memorystream  | false       | .NET 8.0             |   1.964 |  0.04 |  0.1984 |      - |    2496 B |        0.03 |
| XmlSerDe   | DEEP       | memorystream  | true        | .NET 8.0             |   2.141 |  0.04 |  0.2136 |      - |    2680 B |        0.04 |
| System.Xml | DEEP       | memorystream  | false       | .NET Framework 4.7.2 |  71.670 |  1.00 | 18.7988 | 3.0518 |  118510 B |        1.00 |
| XmlSerDe   | DEEP       | memorystream  | false       | .NET Framework 4.7.2 |   6.597 |  0.09 |  0.3967 |      - |    2544 B |        0.02 |
| XmlSerDe   | DEEP       | memorystream  | true        | .NET Framework 4.7.2 |   6.838 |  0.10 |  0.4272 |      - |    2704 B |        0.02 |
| System.Xml | REGULAR    | stringbuilder | false       | .NET 10.0            |   3.685 |  1.00 |  1.7014 | 0.0191 |   21376 B |        1.00 |
| XmlSerDe   | REGULAR    | stringbuilder | false       | .NET 10.0            |   1.075 |  0.29 |  0.5302 | 0.0076 |    6672 B |        0.31 |
| XmlSerDe   | REGULAR    | pooledchar    | true        | .NET 10.0            |   1.168 |  0.32 |  0.1888 |      - |    2384 B |        0.11 |
| XmlSerDe   | REGULAR    | emptystream   | false       | .NET 10.0            |   1.229 |  0.33 |  0.0668 |      - |     840 B |        0.04 |
| System.Xml | REGULAR    | stringbuilder | false       | .NET 8.0             |   4.183 |  1.00 |  1.6937 | 0.0992 |   21272 B |        1.00 |
| XmlSerDe   | REGULAR    | stringbuilder | false       | .NET 8.0             |   1.257 |  0.30 |  0.5302 | 0.0076 |    6672 B |        0.31 |
| XmlSerDe   | REGULAR    | pooledchar    | true        | .NET 8.0             |   1.264 |  0.30 |  0.1888 |      - |    2384 B |        0.11 |
| XmlSerDe   | REGULAR    | emptystream   | false       | .NET 8.0             |   1.464 |  0.35 |  0.0668 |      - |     840 B |        0.04 |
| System.Xml | REGULAR    | stringbuilder | false       | .NET Framework 4.7.2 |   6.812 |  1.00 |  2.2583 | 0.0534 |   14242 B |        1.00 |
| XmlSerDe   | REGULAR    | stringbuilder | false       | .NET Framework 4.7.2 |   3.765 |  0.55 |  1.3809 | 0.0191 |    8706 B |        0.61 |
| XmlSerDe   | REGULAR    | pooledchar    | true        | .NET Framework 4.7.2 |   4.316 |  0.63 |  0.6943 |      - |    4405 B |        0.31 |
| XmlSerDe   | REGULAR    | emptystream   | false       | .NET Framework 4.7.2 |   4.880 |  0.72 |  0.4501 |      - |    2848 B |        0.20 |
| System.Xml | REGULAR    | memorystream  | false       | .NET 10.0            |   3.224 |  1.00 |  0.8354 | 0.0267 |   10488 B |        1.00 |
| XmlSerDe   | REGULAR    | memorystream  | false       | .NET 10.0            |   1.548 |  0.48 |  0.1659 |      - |    2096 B |        0.20 |
| XmlSerDe   | REGULAR    | memorystream  | true        | .NET 10.0            |   1.691 |  0.52 |  0.1774 |      - |    2240 B |        0.21 |
| System.Xml | REGULAR    | memorystream  | false       | .NET 8.0             |   3.850 |  1.00 |  0.8316 | 0.0229 |   10488 B |        1.00 |
| XmlSerDe   | REGULAR    | memorystream  | false       | .NET 8.0             |   1.902 |  0.49 |  0.1640 |      - |    2096 B |        0.20 |
| XmlSerDe   | REGULAR    | memorystream  | true        | .NET 8.0             |   2.095 |  0.54 |  0.1755 |      - |    2240 B |        0.21 |
| System.Xml | REGULAR    | memorystream  | false       | .NET Framework 4.7.2 |   6.909 |  1.00 |  2.2888 | 0.0687 |   14419 B |        1.00 |
| XmlSerDe   | REGULAR    | memorystream  | false       | .NET Framework 4.7.2 |   6.329 |  0.92 |  0.6561 |      - |    4148 B |        0.29 |
| XmlSerDe   | REGULAR    | memorystream  | true        | .NET Framework 4.7.2 |   7.053 |  1.02 |  0.6714 |      - |    4268 B |        0.30 |

**HUGE** (mean in ms):

| Method     | Exhauster     | Preestimate | Runtime              | Mean    | Ratio | Gen0       | Gen1       | Allocated   | Alloc Ratio |
|----------- |-------------- |------------ |--------------------- |--------:|------:|-----------:|-----------:|------------:|------------:|
| System.Xml | stringbuilder | false       | .NET 10.0            | 603.157 |  1.00 | 26000.0000 | 25000.0000 | 582676344 B |       1.000 |
| XmlSerDe   | stringbuilder | false       | .NET 10.0            | 243.181 |  0.40 | 19000.0000 | 18666.6667 | 433981851 B |       0.745 |
| XmlSerDe   | pooledchar    | true        | .NET 10.0            | 164.934 |  0.27 |          - |          - | 216493512 B |       0.372 |
| XmlSerDe   | emptystream   | false       | .NET 10.0            | 142.314 |  0.24 |          - |          - |       924 B |       0.000 |
| System.Xml | stringbuilder | false       | .NET 8.0             | 704.313 |  1.00 | 26000.0000 | 25000.0000 | 582673792 B |       1.000 |
| XmlSerDe   | stringbuilder | false       | .NET 8.0             | 307.843 |  0.44 | 19000.0000 | 18500.0000 | 433981860 B |       0.745 |
| XmlSerDe   | pooledchar    | true        | .NET 8.0             | 185.634 |  0.26 |          - |          - | 216493512 B |       0.372 |
| XmlSerDe   | emptystream   | false       | .NET 8.0             | 186.309 |  0.26 |          - |          - |       840 B |       0.000 |
| System.Xml | stringbuilder | false       | .NET Framework 4.7.2 | 918.217 |  1.00 | 63000.0000 | 22000.0000 | 653938544 B |        1.00 |
| XmlSerDe   | stringbuilder | false       | .NET Framework 4.7.2 | 587.307 |  0.64 | 50000.0000 | 18000.0000 | 519636856 B |        0.79 |
| XmlSerDe   | pooledchar    | true        | .NET Framework 4.7.2 | 507.599 |  0.55 | 13000.0000 |          - | 301795104 B |        0.46 |
| XmlSerDe   | emptystream   | false       | .NET Framework 4.7.2 | 571.083 |  0.62 | 13000.0000 |          - |  85312352 B |        0.13 |
| System.Xml | memorystream  | false       | .NET 10.0            | 468.055 |  1.00 |  3000.0000 |          - | 445112096 B |        1.00 |
| XmlSerDe   | memorystream  | false       | .NET 10.0            | 230.003 |  0.49 |          - |          - | 268327501 B |        0.60 |
| XmlSerDe   | memorystream  | true        | .NET 10.0            | 222.574 |  0.48 |          - |          - | 124792912 B |        0.28 |
| System.Xml | memorystream  | false       | .NET 8.0             | 517.872 |  1.00 |  3000.0000 |          - | 445112096 B |        1.00 |
| XmlSerDe   | memorystream  | false       | .NET 8.0             | 247.999 |  0.48 |          - |          - | 268322032 B |        0.60 |
| XmlSerDe   | memorystream  | true        | .NET 8.0             | 260.799 |  0.50 |          - |          - | 124792912 B |        0.28 |
| System.Xml | memorystream  | false       | .NET Framework 4.7.2 | 688.518 |  1.00 | 19000.0000 |          - | 388763024 B |        1.00 |
| XmlSerDe   | memorystream  | false       | .NET Framework 4.7.2 | 851.533 |  1.24 | 13000.0000 |          - | 353600600 B |        0.91 |
| XmlSerDe   | memorystream  | true        | .NET Framework 4.7.2 | 879.649 |  1.28 | 13000.0000 |          - | 210077336 B |        0.54 |

</details>

### What is measured

Three document shapes:

- **REGULAR** — 26 elements, maximum nesting depth 6, indented, with derived types dispatched by `xsi:type`, an enum, a `DateTime`, and entity-encoded text. This is `ComplexFixture.AuxXml` (POCO form: no CDATA, prefix `xsi` not `p3`), reproduced [at the end of this section](#the-regular-document). The default native host is what the XmlSerDe row measures. `SystemXmlCompatible` on the same document is the `REGULAR_COMPAT` category.
- **DEEP** — one element inside another, 100 levels down, a single string at the bottom, no indentation (`DeepFixture` in `XmlSerDe.Tests/Deep`). The same work in a different shape: wide-and-shallow becomes narrow-and-deep, which is what makes any per-ancestor re-walking visible.
- **HUGE** — mixed document built to ~100 MB (`HugeFixture`). This is the scale where `Preestimate` pays off: a tight rent avoids leaving tens of megabytes of unused buffer, and `MemoryStream` doubling would otherwise dominate the heap.

`.NET Framework 4.7.2` is not there for .NET Framework's own sake — it is the only way to *execute* the netstandard2.0 assemblies, so that row is the `#else` branches being measured.

### How to read these tables

- **Read `Ratio`, not `Mean`.** Every absolute figure here is specific to one machine, one SDK and one OS build. `System.Xml` is code neither project controls, and across earlier runs of this same benchmark its REGULAR deserialize baseline has moved between 7.3 and 9.2 µs. Comparing microseconds across runs mostly measures the machine; comparing a benchmark to the baseline captured beside it does not.
- **Each runtime is its own comparison.** .NET Framework's `System.Xml` is already slower than .NET 10's, so its rows must be read against its own 1.00 and never against .NET 10's. Doing that, XmlSerDe deserialize wins by 1.8× on DEEP and 1.4× on REGULAR there, versus 4.8× and 3.7× on .NET 10.
- **`Alloc Ratio` is the more stable of the two.** Allocation is deterministic — it does not drift with CPU frequency, background load or JIT tiering — so 0.05 on REGULAR deserialize is a firmer claim than any timing on this page.
- **DEEP is a stress shape, not a realistic document.** It exists to make an *O(size × depth)* algorithm impossible to miss; see below.

### What the numbers mean

**Deserialization allocates the object graph, and only the object graph.** 792 B on REGULAR under .NET 10 is the resulting objects — the same property DEEP has, where all 3272 B are the 100 nodes plus their payload string. `Gen0` and `Gen1` tell the part `Allocated` cannot: XmlSerDe triggers no gen1 collection in any of the six REGULAR/DEEP pairs; `System.Xml` triggers one in all six, because its intermediate reader state outlives the gen0 collection that its own allocation rate provokes. Nothing XmlSerDe allocates survives long enough to be promoted — what is left is the object graph, and the caller is still holding that.

Two sources of waste were removed to get there, both found by decomposing an allocation figure rather than by reading code (`GC.GetAllocatedBytesForCurrentThread` deltas around a single warmed-up call, which agree with BenchmarkDotNet to the byte):

| | Before | After |
|---|---:|---:|
| A member with entity references | 232 B | **104 B** |
| A member with two CDATA sections | 384 B | **176 B** |
| REGULAR document, whole deserialize | 1144 B | **808 B** |

`WebUtility.HtmlDecode` takes a `string`, so every text body containing a reference had to be materialized just to be handed over and thrown away — 336 of 1144 bytes on REGULAR, 29%, that never reached the result. `XmlTextDecoder` expands references and CDATA straight out of the span into a buffer (stack below 256 chars, `ArrayPool` above) and materializes exactly once. The current REGULAR row is 792 B; the 808 B figure is the graph-only floor that investigation landed on.

Array members were accumulated in a `List<T>` and copied out with `ToArray()`, costing the list object, its backing array, another array per doubling, and finally the result. `PooledArrayBuilder<T>` takes the intermediate buffers from `ArrayPool<T>`:

| Member | Before | After | The result itself |
|---|---:|---:|---:|
| `int[1]` | 128 B | **56 B** | 32 B + 24 B for the POCO |
| `int[10]` | 304 B | **88 B** | 64 B + 24 B |
| `int[100]` | 1632 B | **448 B** | 424 B + 24 B |
| `int[1000]` | 12472 B | **4048 B** | 4024 B + 24 B |

The "after" column is exactly the array plus the object holding it: the overhead is not reduced but gone. Neither REGULAR nor DEEP has an array member, so this does not appear in those tables — it was measured directly. HUGE does, and the 100 MB deserialize still allocates ~100 MB because that *is* the graph.

**DEEP is in the suite because it once read 9.39.** Deserialization used to be quadratic in nesting depth: 26% faster than `System.Xml` at depth 6, **9.4× slower** at depth 100. To hand back a node, `XmlNode2.GetFirst` first had to know where that node ended, and it found out by recursively parsing the node's entire subtree and discarding the result — so the subtree of a node at depth *d* was re-walked once per ancestor. Instrumentation counted 110 head scans per deserialize of a 26-element document, of which 78 existed only to skip over subtrees, with total character traffic 3.4× the document length.

| Category | Ratio before | Ratio now |
|----------|-------------:|----------:|
| DEEP     | 9.39         | **0.21**  |
| REGULAR  | 0.74         | **0.27**  |

The fix was to stop measuring nodes before parsing them: generated `DeserializeBody` methods now report how much input they consumed, `XmlScan.ReadHead` reads one tag head and never descends, and an unbound element is skipped by a cheap quote-aware tag-balance count instead of being fully parsed. `XmlNode2` remains as the public node-oriented API but is off the hot path. The analysis, the counters and the design are in [docs/perf-single-pass-parser.md](docs/perf-single-pass-parser.md); the earlier investigation that first identified the multiplier is in [docs/perf-redundant-head-scans.md](docs/perf-redundant-head-scans.md). Both also document the benchmarking methodology — including why an A/B switch must be a `static readonly` field read from an environment variable (a plain mutable `static bool` breaks inlining and distorted an entire run by ~1 µs).

Two behaviours changed as a side effect, both strictly less lossy than before: a self-closing child no longer ends the sibling loop (`GetFirstLength` returned length 0 for a bodyless node, which the generated loop read as "no more children", silently dropping everything after `<Foo/>`), and a polymorphic member is now dispatched by member name first and `xsi:type` second.

**HUGE deserialize is the graph at scale.** Under .NET 10 the ratio is 0.45, not 0.27, because the work is dominated by constructing ~100 MB of objects rather than by scanning tags. Allocation 0.39 is the same story: `System.Xml` still pays for the reader and the intermediate tree; XmlSerDe pays for the POCOs. Under .NET Framework the ratio is **1.04** — a few percent slower — while Alloc Ratio stays 0.41. The `#else` branches must `ToString()` every number and date before `Parse`, and on a document that size those strings add up in time even when they do not double the heap.

**Spec compliance is not paid for in the hot loop.** Quote-aware tag-head scanning, the full `S` production for whitespace, and attribute-value normalization initially cost more than intended, because `IndexOfAny("/> \t\r\n")` is six characters and the runtime only vectorizes `IndexOfAny(ReadOnlySpan<T>)` for up to five, falling back to a probabilistic scan beyond that. On net8.0+ that search is now a `SearchValues<char>`, which builds an ASCII bitmap once per process and has no such limit. The netstandard2.0 branch keeps the original workaround: one vectorized `IndexOfAny('/', '>', ' ')` plus a scalar sweep of the short prefix for tab/CR/LF — those are all below `' '` and a legal name character is always above it, so the test is equivalent to a second vectorized search but cheaper than setting one up.

**The netstandard2.0 gap is visible and explainable.** REGULAR allocates 1107 B there against 792 B elsewhere: net8.0+ passes the `ReadOnlySpan<char>` straight into `int.Parse`/`DateTime.Parse`, while netstandard2.0 has no span overloads and must materialize a string per parsed value. DEEP does no parsing at all — one string member at the bottom of the chain — so it stays essentially flat, 3290 B against 3272 B.

**net10.0 and net8.0 compile the same hot path** (`NET8_0_OR_GREATER`). Deserialize DEEP is identical at Ratio 0.21; REGULAR is a little slower on net8 (0.32 vs 0.27). The third target is there to keep that claim honest, and to be where a net9/net10-only API — say `SearchValues.Create(ReadOnlySpan<string>)` for entity names — would land if one were added.

**Serialization is generated `Append` per tag, not a writer over a node tree.** The XmlSerDe rows are exhausters, not separate implementations:

1. **`stringbuilder`** — appends into a `StringBuilder` that grows as needed. `ToString()` copies that buffer into a new `string`, so a large document is paid for twice. On HUGE that is 434 MB against the BCL's 583 MB (Ratio 0.40 / Alloc 0.75 under .NET 10): faster, still two UTF-16 copies.
2. **`pooledchar` + `Preestimate`** — runs `LengthEstimatorExhauster` first, then serializes into `PooledCharExhauster`. One extra walk of the object graph rents a single `char[]` (returned on `Dispose`); `ToString()` copies only the written prefix. On HUGE that is 165 ms and 216 MB — essentially the result string — and **no gen0**. On a tiny document the extra walk can lose: under .NET 8, DEEP `pooledchar` is 1.077 µs against `stringbuilder`'s 0.694 µs, with the allocation win intact (3192 B vs 7832 B).
3. **`emptystream`** — same UTF-8 conversion as the stream path, but `Utf8BinaryExhausterEmpty` keeps a running `Written` count so the JIT cannot delete the encode. Lower bound without I/O: 142 ms and 924 B on HUGE / .NET 10. Under .NET Framework the same row allocates **85 MB**, because the netstandard2.0 encoder still materializes `GetBytes` strings.
4. **`memorystream`** — writes UTF-8 through a 16 KB `ArrayPool` coalesce buffer (`Utf8StreamExhauster`) into a `MemoryStream`. Without `Preestimate` the stream starts empty and doubles; with it, `Capacity` is the estimator times 1.1 (the estimator itself is not padded; Compat's `Serialize(Stream)` does the same 1.1× for a `MemoryStream`). No UTF-16 string is ever built. On HUGE / .NET 10 that cuts allocation from 268 MB to 125 MB at essentially the same speed (230 ms vs 223 ms). On REGULAR the extra walk is visible and not worth it (1.69 µs / 2240 B vs 1.55 µs / 2096 B).

The DEEP serialize ratios (0.01–0.04) look theatrical because the BCL is building a writer and a document for 100 nested elements; the generated code emits the tags. That is the same single-pass property as deserialize, seen from the other side. REGULAR is the honest small-document number: **3.4×** to a `StringBuilder`, **2.1×** to a `MemoryStream`, allocation 0.31 / 0.20.

Under .NET Framework, HUGE `memorystream` is the honest loss: Ratio **1.24** without pre-size and **1.28** with it. UTF-8 conversion without span `GetBytes` / `TryFormat` is more expensive than the BCL's writer on a document that size. Alloc Ratio still falls (0.91 / 0.54). REGULAR `memorystream` there is 0.92 / 1.02 — within noise of a tie on time, still 0.29 on the heap.

### Running the benchmarks

```bash
run-benchmarks.bat
```

The batch file builds in `Release` and prints only the result tables; `dotnet build` output and BenchmarkDotNet's own progress log go to `benchmarks.log`.

`Program.Main` runs two fixtures. They are split because BenchmarkDotNet assigns a job to a whole class — a single `[Benchmark]` cannot pick its own runtime:

| Fixture | Runtimes | What it covers |
|---------|----------|----------------|
| `DeserializeMatrixFixture` | net472, net8.0, net10.0 | `Deserialize` — Categories (REGULAR / REGULAR_COMPAT / HUGE / DEEP) |
| `SerializeMatrixFixture` | net472, net8.0, net10.0 | `Serialize` — Categories (REGULAR / HUGE / DEEP), Exhauster (`stringbuilder` / `pooledchar` / `emptystream` / `memorystream`), Preestimate |

Other fixtures in the project isolate pieces the matrix does not. `AllocationHotspotsFixture` measures the allocation sources that the enum and `Guid` fixes addressed (`Enum.ToString()`, `Enum.Parse` boxing, `StringBuilder.Append(object?)` boxing of `Guid`), each on its own and in small batches so the per-call cost shows up in `Allocated` rather than being lost in a full document. It and `XmlDecodeStringFixture` call APIs that do not exist on netstandard2.0 (`Enum.Parse(Type, ReadOnlySpan<char>)`, `Encoding.GetBytes(string, Span<byte>)`), so they are excluded from the `net472` compile rather than rewritten into measuring something else.

#### The REGULAR document

```xml
<InfoContainer>
    <InfoCollection>
        <BaseInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:type="Derived3Info">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:type="Derived1Info">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:type="Derived2Info">
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

The serializer declaration it is bound to, and the benchmarked entry points:

```csharp
public InfoContainer Deserialize(ReadOnlySpan<char> xml)
{
    XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer r);
    return r;
}

public string Serialize()
{
    var exhauster = new StringBuilderExhauster();
    XmlSerializerDeserializer.Serialize(exhauster, DefaultObject, false);
    return exhauster.ToString();
}

public string Serialize_Pooled()
{
    var estimator = new LengthEstimatorExhauster();
    XmlSerializerDeserializer.Serialize(estimator, DefaultObject, false);

    using var exhauster = new PooledCharExhauster(estimator.EstimatedTotalLength);
    XmlSerializerDeserializer.Serialize(exhauster, DefaultObject, false);
    return exhauster.ToString();
}

public void Serialize_ToEmptyStream()
{
    var be = new Utf8BinaryExhausterEmpty();
    XmlSerializerDeserializer.Serialize(be, DefaultObject, false);
}

public int Serialize_ToMemoryStream()
{
    using var ms = new MemoryStream();
    using (var exhauster = new Utf8BinaryExhausterStream(ms))
    {
        XmlSerializerDeserializer.Serialize(exhauster, DefaultObject, false);
    }
    return (int)ms.Length;
}

[XmlExhauster(typeof(LengthEstimatorExhauster))]
[XmlExhauster(typeof(PooledCharExhauster))]
[XmlExhauster(typeof(StringBuilderExhauster))]
[XmlExhauster(typeof(Utf8BinaryExhausterEmpty))]
[XmlExhauster(typeof(Utf8BinaryExhausterStream))]
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
| **XmlSerDe.Compat** | netstandard2.0; net8.0; net10.0 | Optional. A facade that *is* a `System.Xml.Serialization.XmlSerializer` — see [Drop-in mode](#drop-in-mode-xmlserdecompat). Referenced only if you want it; nothing else depends on it. |
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

This is the fast POCO path: no `[XmlFeatures]`. The generated code reads what XmlSerDe itself writes (`xsi:type`, double-quoted attributes, entity-escaped text). XML extras that neither XmlSerDe nor `System.Xml.Serialization` emit for this subset are opt-in — see [Opt-in XML features](#opt-in-xml-features).

The class **must** be `partial`. On build, the generator emits `OrderSerializer.g.cs` with `Serialize` and `Deserialize` methods.

### 4. Serialize

```csharp
var exhauster = new StringBuilderExhauster();
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

## Drop-in mode: `XmlSerDe.Compat`

The route above is the native one: you declare the serializer class, and every call site names it. `XmlSerDe.Compat` is the other route — for code that already calls `System.Xml.Serialization.XmlSerializer` and would rather not be rewritten. It is **not** a full drop-in replacement; see [What it does not accelerate](#what-it-does-not-accelerate) and [Limitations](#limitations) before reaching for it. On untrusted input it is now the *stricter* of the two routes: the facade turns on the full set of [XML guards](#opt-in-xml-guards) by itself, so an accelerated type refuses the same malformed documents `System.Xml.Serialization` refuses. A native `[XmlSubject]` host in the same assembly does not — it stays lenient until you write `[XmlGuards]`.

Reference the extra project and add one line to your own:

```xml
<ProjectReference Include="..\XmlSerDe.Compat\XmlSerDe.Compat.csproj" />
```

```csharp
global using XmlSerializer = XmlSerDe.Compat.XmlSerializer;
```

That is the whole setup. There is no serializer class to declare and no registration to write: the generator finds every `new XmlSerializer(typeof(T))` whose argument is a literal `typeof`, walks the object graph from `T` transitively — members, collection element types, `[XmlInclude]` derived types, none of which the call site names — and registers the generated delegates from a `[ModuleInitializer]`, before your first line of code runs.

`XmlSerDe.Compat.XmlSerializer` derives from `System.Xml.Serialization.XmlSerializer`, so an instance can be handed to DI, to a field, or to third-party code that has never heard of XmlSerDe:

```csharp
var serializer = new XmlSerializer(typeof(Order));   // resolves to the facade

// fast path: the static type at the call site is the facade
string xml = serializer.SerializeToString(order);
var back = (Order)serializer.Deserialize(xml.AsSpan());

// also works, at a cost — see below
System.Xml.Serialization.XmlSerializer asBcl = serializer;
asBcl.Serialize(Console.Out, order);
```

### When it pays off — and when it doesn't

Two things get cheaper, and they are cheaper for different reasons.

**Per call.** `System.Xml.Serialization` walks a reflection-built plan and materializes an intermediate node tree; the generated code does neither. A round trip of a three-member document (`int`, `string`, `List<string>` of two items), 1000 iterations after warm-up:

| Round trip | Allocated per call |
|---|---|
| facade, `SerializeToString` + `Deserialize(span)` | 1.0 KB |
| facade, `Serialize(TextWriter)` + `Deserialize(TextReader)` | 2.0 KB |
| `System.Xml.Serialization`, same `TextWriter`/`TextReader` | 29.4 KB |

**Per process.** `System.Xml.Serialization` builds a serialization plan for each type on first use — measured cold on the same machine: ~40 ms for the first `XmlSerializer` in the process (that one warms the shared infrastructure), then ~1.5–2 ms of constructor plus ~0.5 ms of first call for every further type. Constructing the facade for an accelerated type instead does a dictionary lookup, and the BCL serializer for that type is never built at all — the fallback field stays null unless something actually needs it.

(Both figures are quick in-process measurements, not BenchmarkDotNet runs; the rigorous numbers for the underlying engine are in [Performance](#performance).)

So it pays off when:

- **you serialize a lot.** Message pumps, per-request payloads, batch jobs — anything where the per-call cost is multiplied by a large number;
- **process startup is on the clock.** CLI tools, serverless functions, desktop app launch: a project with a dozen serialized types pays tens of milliseconds to `System.Xml.Serialization` before doing any work, and accelerated types skip it;
- **allocation rate is the problem**, not raw speed — a service whose gen0 collections are driven by XML traffic;
- **documents are deeply nested.** Deserialization is single-pass, so cost does not grow with depth ([~4.8× on a 100-level document](#performance)).

It buys you nothing — or close to nothing — when:

- **the types are refused.** See [What it does not accelerate](#what-it-does-not-accelerate); the code still works, at exactly the speed it had before;
- **the calls go through `XmlWriter` / `XmlReader` / `XmlSerializerNamespaces`.** Those paths materialize the document or hand the call over entirely — see [What the fast path costs elsewhere](#what-the-fast-path-costs-elsewhere). If that is *all* your code does, the facade adds a layer and returns nothing;
- **serialization happens once, at startup, on a small config file.** Saving 2 ms on a 300 ms boot is not a reason to add a dependency;
- **the bytes must match `System.Xml.Serialization` exactly** — see [Where the output differs](#where-the-output-differs-from-systemxmlserialization);
- **the input is untrusted and you are on the native route without `[XmlGuards]`.** The default engine does not validate well-formedness. The facade does: it enables the full guard set itself, so this caveat applies to a native host that has not asked for the guards, not to the drop-in.

A useful way to decide: the facade helps in proportion to how much of your XML work already goes through the fast overloads on types the generator accepts. Measure that share first — [Verifying the migration](#verifying-the-migration) shows how to get the list.

### What it does not accelerate

A type that the generator will not serve is not an error: it falls back to `System.Xml.Serialization.XmlSerializer` and keeps working, just without the speed-up. The generator refuses a type **whole** rather than emitting almost-correct code for it, so a refusal is triggered by any of:

- any type in the graph is not a class (a `struct` is refused, though the BCL supports it), or is generic, or — unless `abstract` — has no accessible parameterless constructor;
- an `abstract` type declares no `[XmlInclude]`, or is the root itself: dispatch by `xsi:type` at the top of the document is not supported;
- a member type is a collection other than `List<T>` or `T[]` (a `HashSet<T>` is refused, though the BCL supports it), or a nested collection (`List<List<int>>`), or any other generic;
- anything in the graph asks for a **namespace** — `Namespace =` on `[XmlRoot]`, `[XmlType]`, `[XmlElement]`, `[XmlAttribute]`, `[XmlArray]` or `[XmlArrayItem]`. XmlSerDe writes namespace declarations as fixed literals and would produce a document the BCL cannot read back;
- a type implements `IXmlSerializable`, whose `ReadXml`/`WriteXml` decide the document shape that the generator would otherwise override by walking members;
- a member carries `[XmlChoiceIdentifier]`, `[XmlAnyElement]`, `[XmlAnyAttribute]` or `[XmlNamespaceDeclarations]`, or picks its element name by value type (several `[XmlElement]` on one member, or one with a `typeof`);
- a member sets `DataType =` on `[XmlElement]`, `[XmlAttribute]`, `[XmlArrayItem]` or `[XmlText]`. That property picks the lexical form, not the name: with `DataType = "date"` the BCL writes `2020-01-02` instead of a full `dateTime`, and with `"hexBinary"` it writes `01FF` instead of base64;
- an enum anywhere in the graph is marked `[Flags]`. The BCL writes a combination as an xsd list — `Read Write` — while the generated code falls back to `Enum.ToString()`, which produces `Read, Write`; the BCL then refuses to read that back at all (`Instance validation error: 'Read,' is not a valid value`). Both halves are measured, in both directions.

The last five are a different species from the rest: an unsupported *type* stops code generation, whereas an unsupported *attribute* would have produced perfectly valid code and a different document, with `IsAccelerated` reporting `true`. They are refusals precisely so that they cannot be silent.

Some *call sites* aren't accelerated either: `new XmlSerializer(someTypeVariable)` cannot be resolved at build time and is not seen at all — the type has to be a literal `typeof`. Three shapes are recognized:

```csharp
new XmlSerializer(typeof(Order))
XmlSerializer.FromTypes(new[] { typeof(Order), typeof(Invoice), })   // array written in place
new XmlSerializerFactory().CreateSerializer(typeof(Order))           // XmlSerDe.Compat.XmlSerializerFactory
```

Some *paths* aren't accelerated either — see the table below. `XmlSerDe.Compat.XmlSerializer.IsAccelerated` answers the question for an instance you already hold.

Every refusal is reported, because "why didn't mine get faster" should have an answer:

| Diagnostic | Meaning | Default severity |
|---|---|---|
| `XMLSERDE001` | this type falls back, with the reason and the call-site location | `Info` |
| `XMLSERDE002` | code generation was abandoned after the graph walk had accepted the type — a gap in the generator, not a refusal by design | `Warning` |

Refusing one type never affects the others. If your project depends on the acceleration, make the fallback loud:

```xml
<PropertyGroup><XmlSerDeCompatStrict>error</XmlSerDeCompatStrict></PropertyGroup>
<ItemGroup><CompilerVisibleProperty Include="XmlSerDeCompatStrict" /></ItemGroup>
```

`warning` and `true` raise `XMLSERDE001` to a warning; `error` fails the build. The strict setting changes only the volume, never the decision — the same types are accelerated either way. `CompilerVisibleProperty` is required: without it the generator cannot see the property.

### What the fast path costs elsewhere

The allocation figures in [Performance](#performance) are for the span API, and the facade does not extend them to every overload:

| Call | Path |
|---|---|
| `SerializeToString`, `Deserialize(ReadOnlySpan<char>)` | generated code; the string path estimates length, writes into `PooledCharExhauster`, and copies only the written prefix |
| `Serialize(TextWriter)` | estimated `PooledCharExhauster`, then `WriteTo` the writer — no extra `ToString()` |
| `Serialize(Stream)` | UTF-8 through `Utf8StreamExhauster` (16 KB coalesce buffer); a `MemoryStream` is pre-sized to 1.1× the char estimate |
| `Deserialize(TextReader)`, `Deserialize(Stream)` | the input is read into a rented `char[]` (`PooledCharText`), then parsed from its span |
| `Serialize(XmlWriter)`, `Deserialize(XmlReader)` — i.e. any call through the base type | same materialization, plus the `XmlWriter`/`XmlReader` themselves: the document goes out through `WriteRaw` and comes in through `ReadOuterXml` |
| `Serialize(…, XmlSerializerNamespaces)` | handed to `System.Xml.Serialization` in full |
| any overload with a non-null `encodingStyle` | handed to `System.Xml.Serialization` in full — which throws, exactly as it does for any serializer not built through `SoapReflectionImporter` |
| `Deserialize(XmlReader, XmlDeserializationEvents)` with any handler set | handed to `System.Xml.Serialization` in full, so the events fire |
| `CanDeserialize` | answered from the registry — the question is "does the root element have this name", and the generated code already knows the name |
| any `Deserialize` on an instance with a subscriber on `UnknownNode` / `UnknownElement` / `UnknownAttribute` / `UnreferencedObject` | handed to `System.Xml.Serialization` in full, so the events fire (see [below](#where-the-output-differs-from-systemxmlserialization)) |

The `XmlSerializerNamespaces` overloads are given away rather than approximated: XmlSerDe writes namespace declarations as fixed literals and cannot honor a caller-supplied set, so producing a document without the requested declarations would be worse than being slow.

If you have no reference to `XmlSerDe.Compat`, none of this exists: the generator emits nothing for compatibility, which is asserted by a test rather than promised.

### Where the output differs from `System.Xml.Serialization`

Both sides read each other's documents — that is what the [differential harness](#test-coverage-map) checks on every build, in both directions. But the two writers do not produce the same *text*, and if anything downstream compares XML byte-for-byte, this is the part that will surprise you. The same object, written by each side:

```xml
<!-- System.Xml.Serialization, Serialize(TextWriter) -->
<?xml version="1.0" encoding="utf-16"?>
<Order xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Id>7</Id>
  <CustomerName>ACME</CustomerName>
</Order>

<!-- XmlSerDe.Compat, same call -->
<?xml version="1.0" encoding="utf-16"?><Order><Id>7</Id><CustomerName>ACME</CustomerName></Order>
```

Two differences, both deliberate:

- **No indentation.** XmlSerDe writes the document compactly; `System.Xml.Serialization` indents with two spaces and CRLF. Insignificant whitespace, but not identical bytes.
- **No `xmlns:xsi` / `xmlns:xsd` on the root.** The BCL declares both prefixes on every document whether or not they are used; XmlSerDe declares `xmlns:xsi` exactly where it writes `xsi:type` or `xsi:nil`, and nowhere else. Both are legal, and each side reads the other's form.
The declared encoding is *not* one of them: like the BCL, the facade takes it from the sink — `TextWriter.Encoding.WebName`, so a `StringWriter` gets `utf-16` and a `StreamWriter` over ASCII gets `us-ascii`. `Serialize(Stream)` chooses no encoding at all and always declares `utf-8`; that is what .NET (Core) does, while .NET Framework writes `<?xml version="1.0"?>` there, and following the modern behavior is deliberate.

`SerializeToString` is the one place that always declares `utf-8`, on both the fast and the fallback path: it owns its sink, and `utf-8` is the only useful claim about a string that is about to be written to a file. Pass `appendXmlDeclaration: false` if you would rather write the prolog yourself — that also works on both paths.

Beyond the text, one behavioral difference worth knowing before you migrate:

- **Subscribing to `UnknownNode` / `UnknownElement` / `UnknownAttribute` / `UnreferencedObject` turns the acceleration off for deserialization on that instance.** The events cannot be raised from the fast path at all: `XmlElementEventArgs` and its siblings have no public constructor, and the method the base class uses to hand the subscriber list to a reader is internal — the arguments simply cannot be constructed outside `System.Xml.Serialization`. So a subscription means the whole document goes through the BCL, which raises the events itself with a real `XmlElement`, a line number and the list of expected elements. Silence where the caller asked to be told would be the worse trade. `IsDeserializationAccelerated` reports the current state; unsubscribing restores the fast path, and serialization is never affected. Passing the handlers straight into the call — `Deserialize(reader, events)` — is covered the same way.

- **What a base-typed reference cannot carry.** The overloads that decide these two questions (`Deserialize(XmlReader, XmlDeserializationEvents)`, and everything taking an `encodingStyle`) are not virtual on `System.Xml.Serialization.XmlSerializer`, so the facade redeclares them with `new` — which works exactly where the static type at the call site is the facade, i.e. the drop-in scenario the alias creates. Through a base-typed reference the base class's own overload runs: a subscription made there (`((System.Xml.Serialization.XmlSerializer)s).UnknownElement += …`) still gets silence, and a non-null `encodingStyle` still writes an ordinary document where the BCL would throw (measured, and pinned by a test so it cannot change unnoticed). Closing this would mean giving up on being a drop-in.

Null and exceptions, on the other hand, behave like the BCL on purpose. `null` is written as `<T xsi:nil="true" />` and read back as `null`, because the fast path hands nulls to `System.Xml.Serialization` rather than inventing an answer. And a failure on the fast path is wrapped exactly as the BCL wraps it — `InvalidOperationException` with the real cause as `InnerException`, so a `catch (InvalidOperationException)` written before the migration keeps catching:

| Failure | `System.Xml.Serialization` | `XmlSerDe.Compat` |
|---|---|---|
| wrong object type to `Serialize` | `InvalidOperationException("There was an error generating the XML document.")` → `InvalidCastException` | same |
| malformed document to `Deserialize` | `InvalidOperationException("There is an error in XML document (1, 25).")` → `XmlException` | same chain, message without the position (see below): the facade runs the full [guard set](#opt-in-xml-guards), and a document error arrives as `XmlException` inside, not as our own `InvalidOperationException` |
| `null` writer or stream | `ArgumentNullException` (`output` / `input`) | same |

Three details behind "same", all measured against the BCL rather than assumed:

- **no line and position on the fast path.** The BCL names them because it reads through an `XmlReader`; XmlSerDe parses a span and has nothing to report. The message is then the BCL's own no-position form, `There is an error in the XML document.` — which it uses for a reader without `IXmlLineInfo`.
- **one extra wrapping layer through the base type.** `System.Xml.Serialization.XmlSerializer` wraps twice around the pre-generated-assembly contract it calls the facade through, so a call via a base-typed reference yields `InvalidOperationException` → `InvalidOperationException` → the real cause. Both layers are above the facade's own code; the outer type and message still match, and any pre-generated `XmlSerializers.dll` behaves the same way.
- **modern .NET is the reference for the message text and for null arguments.** On .NET Framework the BCL serves localized resources (so its text follows the machine's UI culture, while the facade's literal is always English), throws `NullReferenceException` rather than `ArgumentNullException` for a null `TextWriter` or a null input `Stream`, and names the `Serialize(Stream)` parameter `stream` instead of `output`. The facade follows .NET Core on all targets — reproducing a bug fixed upstream is not compatibility.

### Migrating an existing project

Work one project at a time — the `global using` is per compilation, so the blast radius of each step is one assembly.

**1. Pick a project and add the reference.**

```xml
<ProjectReference Include="..\XmlSerDe.Compat\XmlSerDe.Compat.csproj" />
<ProjectReference Include="..\XmlSerDe.Common\XmlSerDe.Common.csproj" />
<ProjectReference Include="..\XmlSerDe.Components\XmlSerDe.Components.csproj" />
<ProjectReference Include="..\XmlSerDe.Generator\XmlSerDe.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

**2. Take the inventory before switching anything.** Add the alias in a scratch commit, then build once with the fallback made loud — the default severity of `XMLSERDE001` is `Info`, which `dotnet build` does not print at any verbosity:

```xml
<ItemGroup><CompilerVisibleProperty Include="XmlSerDeCompatStrict" /></ItemGroup>
```

```bash
dotnet build -p:XmlSerDeCompatStrict=warning
```

Every refused type shows up as a warning pointing at the *call site*, with the reason:

```
Program.cs(13,19): warning XMLSERDE001: 'global::Bad' falls back to
System.Xml.Serialization.XmlSerializer: Set: HashSet<int> is not a supported generic type
```

That list is the decision: if the types you actually care about are all on it, stop here and drop the branch — this project is not a candidate.

**3. Switch the alias on**, in one file:

```csharp
global using XmlSerializer = XmlSerDe.Compat.XmlSerializer;
```

Anything that names `System.Xml.Serialization.XmlSerializer` by its full name is unaffected — the alias only rebinds the short name, so a partially migrated file keeps compiling.

**4. Find the call sites the generator cannot see.** `new XmlSerializer(t)` with a variable or a `Type` from configuration produces no acceleration and no diagnostic — there is nothing to report. Where the type is statically known, spell it out:

```csharp
// invisible to the generator
static XmlSerializer For(Type t) => new XmlSerializer(t);

// visible
static XmlSerializer ForOrder() => new XmlSerializer(typeof(Order));
```

**5. Expect the extra constructors to compile, not to accelerate.** `XmlSerializer(Type, string)`, `(Type, Type[])`, `(Type, XmlAttributeOverrides)`, `(Type, XmlRootAttribute)` and the five-argument overload all exist on the facade, so a `global using` does not break call sites that use them. What they do *not* do is accelerate: a namespace cannot be declared by XmlSerDe, `XmlAttributeOverrides` rewrites markup the generator already resolved at build time, and `extraTypes` introduces derived types the generated code has never heard of. Such an instance hands everything to `System.Xml.Serialization` — built with exactly the arguments you passed — and `IsAccelerated` says so. An *empty* extra argument (`null`, `""`, `Type.EmptyTypes`) is not an extra argument and keeps the fast path.

**5a. If you build serializers through a factory, alias it too.**

```csharp
global using XmlSerializerFactory = XmlSerDe.Compat.XmlSerializerFactory;
```

`XmlSerializer.FromTypes(new[] { typeof(Order), })` is already covered by the first alias — the facade redeclares it and the generator reads the types out of an array written in place. The factory needs its own line, because `CreateSerializer` is a different type's method; with the alias, a registered type comes back as an accelerated facade and everything else comes back from the base factory, cache and all. Without the alias both keep working exactly as before, just without acceleration.

**6. Move the hot calls onto the fast overloads.** Existing code keeps working untouched, but `SerializeToString(obj)` and `Deserialize(span)` are where the numbers above come from. Passing an `XmlWriter` or `XmlReader` works and is correct — it just gives up most of the win.

**7. Turn on strict mode once the list is empty**, so a future edit that quietly costs you the acceleration — a new `Dictionary<,>` member, a type that loses its parameterless constructor — fails the build instead of getting slower:

```xml
<PropertyGroup><XmlSerDeCompatStrict>error</XmlSerDeCompatStrict></PropertyGroup>
```

### Verifying the migration

The migration is a no-op for correctness if all four of these hold. Check them in order — each catches a different failure.

**1. The build is clean under strict mode.**

```bash
dotnet build -p:XmlSerDeCompatStrict=error
```

No `XMLSERDE001` means every call site the generator *saw* was accepted — it says nothing about the ones it never saw, which is what check 2 is for. `XMLSERDE002` means the graph walk accepted a type the code generator then choked on: that is a bug in XmlSerDe, not in your code — the affected types fall back and keep working, and the message is worth reporting.

**2. The types you expect are actually registered**, at runtime, where a refactor can't silently undo it:

```csharp
var serializer = new XmlSerializer(typeof(Order));
Debug.Assert(serializer.IsAccelerated, "Order is no longer served by the generated code");
```

`IsAccelerated` is the only way to tell the two paths apart from the outside — a fallback is invisible otherwise, which is exactly why the property exists.

**3. Old documents still read, and new documents still parse elsewhere.** Round-tripping through XmlSerDe alone proves nothing: two consistent-but-wrong halves cancel out. Compare against the BCL in both directions, which is what the project's own harness does:

```csharp
var ours = new XmlSerializer(typeof(Order));
var bcl = new System.Xml.Serialization.XmlSerializer(typeof(Order));

// they read our documents
var theirs = (Order)bcl.Deserialize(new StringReader(ours.SerializeToString(order)));

// we read theirs
var writer = new StringWriter();
bcl.Serialize(writer, order);
var mine = (Order)ours.Deserialize(writer.ToString().AsSpan());
```

Keep a corpus of real documents from production for this — the shapes that break interop are the ones nobody thought to write a POCO for.

**4. Nothing downstream depends on the exact bytes.** Grep for golden-file comparisons, XML stored as a string and compared for equality, checksums over serialized payloads, and schema validation that expects the `xsd`/`xsi` declarations. See [Where the output differs](#where-the-output-differs-from-systemxmlserialization) — this is the failure that shows up in someone else's test suite, not yours.

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

Registers an `IExhauster` implementation. The generator emits a `Serialize` overload for each registered exhauster. If omitted, `StringBuilderExhauster` is used automatically.

### `[XmlInjector(typeof(T))]`

Registers an `IInjector` implementation. The generator emits a `Deserialize` overload for each registered injector. If omitted, `DefaultInjector` is used automatically.

### `[XmlFactory(typeof(T), invocationStatement)]`

Replaces `new T()` during deserialization with a custom C# expression. Useful for object pooling and reuse of already-allocated instances.

```csharp
[XmlFactory(typeof(InfoContainer), "global::MyApp.CachedInfoContainer.Reuse()")]
```

The factory type must provide a `Reset()`-style method that clears state before reuse. See `CachedInfoContainer` in `XmlSerDe.Tests/Complex/Subject/InfoContainer.cs`.

### Opt-in XML features

`[XmlFeatures(XmlFeature.…)]` — XML 1.0 extras that the POCO path does not need. The attribute goes on the **serializer class** (same place as `[XmlSubject]`), not on each subject type. Several attributes on one class are OR-combined.

| Flag | What it accepts / does | When to turn it on |
|------|------------------------|--------------------|
| `Markup` | `<!-- … -->`, `<?…?>` and `<!DOCTYPE …>` between elements and in the prolog (not the XML declaration) | Foreign documents with comments, PIs or a DOCTYPE |
| `CData` | `<![CDATA[…]]>` in element text and between elements; implies `Markup` | Foreign documents that use CDATA instead of entities |
| `FlexibleXsiPrefix` | `xmlns:*=XMLSchema-instance` then that prefix for `type`/`nil` | Documents that bind the instance namespace to something other than `xsi` (tests used to write `p3`) |
| `CharGuard` | `XmlCharGuard` before escaping a string on serialize | Reject illegal XML 1.0 `Char` (e.g. `U+0001`) instead of writing it |
| `SystemXmlCompatible` | All of the above | Yesterday's native behaviour, in one flag |

Entities (`&amp;`, CharRef), `xsi:type` / `xsi:nil` **as the `xsi` prefix**, collections, and `S` whitespace between elements stay on. They are the POCO format, not extras. So are quoted attributes: `'` as well as `"`, and a `>` inside an `AttValue` that does not close the tag (XML 1.0 §2.4). That used to be a flag; it is now the only behaviour, because the measurement below put its price at the noise floor while the flagless path could mis-read such a head silently.

Comments, PIs and DOCTYPE are **one** flag rather than three for the same reason in reverse: they run through the same `ReadHeadMarkup` / `SkipBodyMarkup` code, so the first of them costs ~15% and the other two are free. Splitting them bought a strictness nobody asked for at the price of a combinatorial surface in the generator.

Narrow include — only CDATA, prefix already `xsi`:

```csharp
[XmlFeatures(XmlFeature.CData)]
[XmlSubject(typeof(Order), true)]
public partial class OrderSerializer { }
```

Full previous native set:

```csharp
[XmlFeatures(XmlFeature.SystemXmlCompatible)]
[XmlSubject(typeof(Order), true)]
public partial class OrderSerializer { }
```

Compat does not need the attribute: the generated facade host is `SystemXmlCompatible` internally. A `[XmlSubject]` class in the same assembly does **not** inherit those flags.

Disabled feature is an **error**, not a skip: a comment on a default host throws, it is not ignored.

`CData` is the only flag with an effect beyond the scanner: `IInjector` has no CDATA-aware overload, so a `CData` host decodes string members directly and a custom injector is not called for them. `Markup` alone keeps the injector.

The prolog follows the same rule. Each generated serializer gets its own `CutXmlHead`, so the flag never has to be repeated by hand:

```csharp
//snips <?xml ...?> always; the DOCTYPE only because this host enabled Markup
var body = OrderSerializer.CutXmlHead(xml.AsSpan());
OrderSerializer.Deserialize(DefaultInjector.Instance, body, out Order order);
```

### Opt-in XML guards

`[XmlGuards(XmlGuard.…)]` — refusal on input that XML 1.0 calls not-well-formed and that the POCO path otherwise turns into an object anyway. Same place as `[XmlFeatures]` (the **serializer class**), same OR rule for several attributes, same "no attribute ≡ `None`".

The two axes are orthogonal and deliberately not one enum: `XmlFeature` answers *should this construct be understood*, `XmlGuard` answers *should this be refused because XML 1.0 forbids it*. A host can have `CData` and no guards, or every guard and no features.

| Flag | What it rejects | What the default does instead |
|------|-----------------|-------------------------------|
| `MatchingEndTags` | A closing tag that does not match the open one, and a complex type whose body ends at end-of-input | Any `</…>` closes the current class; a truncated document succeeds with a partially filled object |
| `SingleRoot` | A second root element, or garbage after the first one | Everything past the first element is silently eaten |
| `UniqueAttributes` | Two attributes with one qualified name on a head (XML 1.0 WFC: Unique Att Spec) | First match wins |
| `IllegalChars` | A string that reached the POCO (element text, attribute value) containing a character outside XML 1.0 §2.2 `Char` | `U+0001` in a string member is accepted |
| `SystemXmlCompatible` | All of the above | — |

```csharp
//narrow: someone else's writer emits a stray closing tag and it must not pass
[XmlGuards(XmlGuard.MatchingEndTags)]
[XmlSubject(typeof(Order), true)]
public partial class OrderSerializer { }

//full: refuse what System.Xml.Serialization refuses
[XmlGuards(XmlGuard.SystemXmlCompatible)]
[XmlSubject(typeof(Order), true)]
public partial class StrictOrderSerializer { }
```

`XmlSerDe.Compat` enables the full set by itself — the facade is a drop-in for `System.Xml.Serialization.XmlSerializer`, and being more lenient than the thing it replaces is not a feature. A `[XmlSubject]` class in the same assembly inherits nothing.

**Exceptions.** A host with any guard reports a document error the way the BCL does over a reader without `IXmlLineInfo`: `InvalidOperationException("There is an error in the XML document.")` with the real cause in an `XmlException` inside — the same chain, so a `catch (XmlException)` written before the migration keeps catching. There is no line or position: the parser walks a span and has nothing to report. A host **without** guards keeps today's plain `XmlDocumentException` (it derives from `InvalidOperationException`) with today's message.

**What guards are not.** Not an `XmlReader` pre-pass — the checks are melted into the cursor that was walking the document anyway, and a default host does not call a single one of them. Not a schema. Unknown elements are still skipped: that is the data-binding contract, not a hole. And three things stay unchecked even with the full set, on purpose:

- closing tags **inside a skipped foreign subtree** — counting names there needs a stack as deep as the subtree, which is the very cost that was refused for the end-of-file message;
- a duplicate attribute reached through **two different prefixes** bound to the same namespace (`xsi:type` and `p3:type`) — comparison is on the literal qualified name, since the core has no general prefix resolution;
- the **line and position** in the exception.

**Always on, no flag.** Attribute syntax is checked on every host, including the default one: `Attribute ::= Name Eq AttValue` with `Eq ::= S? '=' S?`. There is no mode in which reading the *neighbouring* attribute's value is right, and until this became a check that is what happened — `<Foo id=1 Tag="x">` was read as `id="x"` with `Tag` lost, and `<Foo id Tag="x">` produced an attribute literally named `"id Tag"`. The same check fixes the mirror-image bug on **valid** input: `<Foo a = "1">` is legal XML that XmlSerDe used to drop silently, because the name ended only at `=`, `:`, `/` or `>` and so came out as `"a "`.

### Cost of turning a flag on

```bash
dotnet run -c Release -f net10.0 --project XmlSerDe.PerformanceTests -- --feature-cost
```

The probe measures a ladder of hosts (`XmlSerDe.Tests/Complex/FeatureLadderHosts.cs`) that differ **only** in `[XmlFeatures]` — same type graph, same exhauster, same input. The input is the REGULAR document (`ComplexFixture.AuxXml`) and contains none of the opt-in constructions, so what is measured is the price of *being able* to understand them, not the price of a different document. REGULAR is the most telling shape available: 26 elements pay `ReadHead` per tag, three `xsi:type` attributes pay attribute parsing and prefix lookup, and the entity-escaped strings pay the decoder and — on serialize — the char guard.

It is not a BenchmarkDotNet job: the process pins itself to one core and raises its priority, every case is warmed up, then all cases are timed round-robin (direction alternating) for fifteen rounds and the best time of each is kept. Two facts serve as the built-in noise check, and both hold in the numbers below: `CharGuard` must not move deserialization at all, and no other flag may move serialization at all. Anything under ~3% is noise. Numbers are the median of three runs on net10.0, 13th Gen Intel Core i7-13700H. Absolute times drift a few percent between runs; only ratios measured inside one round-robin are comparable.

**One flag at a time, over the flagless baseline:**

| Host | Deserialize | Serialize | Where it goes |
|------|------------:|----------:|---------------|
| default (no flags) | 1.00 (~2.0 µs) | 1.00 (~715 ns) | |
| `Markup` | **1.14×** | 1.00× | `ReadHeadMarkup` / `SkipBodyMarkup` / `SkipToTagMarkup`: `<!`/`<?` becomes a skip instead of an error, on every tag |
| `CData` | **1.14×** | 1.00× | same markup path, plus the CDATA-aware decoder on every string |
| `FlexibleXsiPrefix` | **1.10×** | 1.00× | xmlns-URI scan over the attributes of every head that has any |
| `CharGuard` | 1.00× | **1.14×** | `AppendEncoded` instead of `AppendEncodedUnchecked` — one extra pass over each string |

Deserialization-side flags do not touch the serializer at all: the generated `Serialize` is byte-identical for every host except the `CharGuard` one. The 1.00× column is a measurement of that fact, not a rounding.

**Cumulatively, in enum order — the marginal column is what each step adds to the one above it:**

| Step | Deserialize | vs previous | Serialize | vs previous |
|------|------------:|------------:|----------:|------------:|
| default (no flags) | 1.00 | — | 1.00 | — |
| `+ Markup` | 1.15× | **+15%** | 1.00× | — |
| `+ CData` | 1.16× | ~0 | 1.00× | — |
| `+ FlexibleXsiPrefix` | 1.28× | **+11%** | 1.00× | — |
| `+ CharGuard` = `SystemXmlCompatible` | 1.27× | ~0 | **1.17×** | **+17%** |

Three prices, and the table now has one row per price. `Markup` is the cost of entering the markup-aware scan (~15%); `CData` rides on it for free and pays only its own decoder; `FlexibleXsiPrefix` is an extra attribute scan per head (~11%); `CharGuard` is one extra pass per string on serialize (~15%). Nothing here is cheap enough to enable "just in case", and nothing is expensive enough to split further.

Quote-aware heads used to be the fifth row, at **1.03×** measured the same way — the only flag whose price sat at the noise floor. It is no longer a flag; that ~3% is now part of the default, in exchange for the default no longer truncating a head at a `>` inside an attribute value.

**Whole documents, same probe:**

| Case | Time | Allocated |
|------|-----:|----------:|
| REGULAR deserialize, default | ~2.0 µs | 792 B |
| REGULAR deserialize, `SystemXmlCompatible` | ~2.6 µs (1.25×) | 792 B |
| REGULAR deserialize, `SystemXmlCompatible` on the pre-opt-in document (CDATA + `p3:type`) | ~2.5 µs | 792 B |
| REGULAR serialize, default | ~715 ns | 0 B |
| REGULAR serialize, `SystemXmlCompatible` | ~825 ns (1.15×) | 0 B |

The REGULAR matrix also has a `REGULAR_COMPAT` category (`XmlSerializerDeserializerCompatible` on the same `AuxXml`) for a BenchmarkDotNet comparison of default vs `SystemXmlCompatible` on the large document.

### Cost of turning a guard on

```bash
dotnet run -c Release -f net10.0 --project XmlSerDe.PerformanceTests -- --guard-cost
```

Same instrument and same reasoning as the flag table above: the question is not "what does deserialization cost" but "what does *one guard* add", and that is a single-digit percentage. A second ladder of hosts (`XmlSerDe.Tests/Complex/GuardLadderHosts.cs`) differs **only** in `[XmlGuards]` — same type graph, same exhauster, same input. The input contains no violation at all: what is measured is the price of *being able* to refuse, not the price of refusing.

**REGULAR** (`ComplexFixture.AuxXml`: 26 elements, three heads carrying `xmlns:xsi` + `xsi:type`, entity-escaped strings), one guard at a time over the default host:

| Flag | Deserialize | Allocated | Where the time goes |
|------|------------:|----------:|---------------------|
| `MatchingEndTags` | 1.02–1.05× | 816 B | one name comparison per element body, plus the expected name travelling into `DeserializeBody` |
| `SingleRoot` | ~1.00× | 816 B | one scan of an empty tail per document; inside the noise floor |
| `UniqueAttributes` | **1.15×** | 816 B | one walk over the attributes of a head that has any — and every attribute-carrying head here holds a 41-character `xmlns:xsi` value the walk has to step over |
| `IllegalChars` | **1.06×** | 816 B | one extra pass over every string that reaches the POCO |

**Cumulatively** (median of six runs; the spread of the last row across those runs was 1.22–1.38×):

| Step | Deserialize | vs previous | Allocated |
|------|------------:|------------:|----------:|
| default (no guards) | 1.00 | — | 816 B |
| `+ MatchingEndTags` | 1.03× | +3% | 816 B |
| `+ SingleRoot` | 1.03× | ~0 | 816 B |
| `+ UniqueAttributes` | 1.19× | **+16%** | 816 B |
| `+ IllegalChars` = `SystemXmlCompatible` | **1.25×** | **+5%** | 816 B |

The `Allocated` column is a statement, not a measurement of interest: it is byte-identical down the ladder because a guard is not allowed to allocate on the happy path — no list of seen attribute names, no reader. If that column ever moves, something grew a heap.

**WIDE** — the same REGULAR document with ten extra attributes on every head, none of which the host knows. The parser ignores them (it looks its own up by name); `UniqueAttributes` must walk all of them, so this is the row that decides how the uniqueness check is implemented:

| Flag | Deserialize | Allocated |
|------|------------:|----------:|
| `UniqueAttributes` | **1.40×** | 816 B |
| `SystemXmlCompatible` | **1.46×** | 816 B |

Already-seen names are kept as `(offset, length)` pairs in a `stackalloc Span<int>`, so each head is parsed once. The obvious alternative — compare each attribute against the earlier ones by re-parsing them — is quadratic in the number of attributes, and on this document costs **2.37×** instead of 1.40× (three runs each, 2.35/2.42/2.37 against 1.40/1.41/1.39; no overlap). On a POCO-shaped head with two attributes the two implementations are indistinguishable — one extra parse, well under the drift — so the buffer buys nothing there and everything here. Spans themselves cannot be stored (`Span<ReadOnlySpan<char>>` is illegal — `roschar` is a ref struct) and a heap list is forbidden on this path; two ints per name are neither. A head with more names than the buffer holds falls back to the pairwise walk: overflow is not a duplicate, and a head with 33 distinct attributes is legal XML.

**DEEP** (100 nested levels, no attributes, one string) — every row lands inside the ±4% spread of the probe on this document, `SystemXmlCompatible` included. Nothing there costs anything measurable: `UniqueAttributes` never fires (no head has attributes), `IllegalChars` has one string to check, `SingleRoot` one empty tail, and `MatchingEndTags` compares a short name once per level against a body that took ~30 ns to parse anyway.

Two honest notes about this table:

- the full set on REGULAR is **~1.25×**, not the "5–10%" that `docs/opt-in-xml-guards.md` §3 estimated before the work, and two thirds of that is `UniqueAttributes` alone. The estimate assumed heads with short attributes; REGULAR's heads carry a 41-character namespace URI, and enumerating the attributes means stepping over it. It is still nowhere near the 2× that would mean a forbidden full document scan, and a document with no attributes (DEEP) pays nothing;
- the numbers are ratios measured **inside one round-robin**. Absolute times drift several percent between processes on this machine, so a cross-process A/B of the default host is not evidence of anything — which is exactly why the always-on attribute check and the scalar closing-tag check are not in this table.

### Cost of the `xsi:type` lookup

```bash
dotnet run -c Release -f net10.0 --project XmlSerDe.PerformanceTests -- --attr-cost
```

Neither of the tables above says anything about a POCO whose attributes carry *data*: REGULAR has attributes on three heads out of twenty-six and all of them are plumbing (`xmlns:xsi`, `xsi:type`), with no `[XmlAttribute]` members anywhere. So there is a third document, **ATTRS** — thirty nodes, three `[XmlAttribute]` members each.

That shape is walked repeatedly. Per head the generated code emits `ReadHead` (its own scan to the unescaped `>`), then the `xsi:type` lookup, then one lookup per `[XmlAttribute]` member, each starting over from the front of the head. Measured by making one walk happen twice — which keeps correctness and control flow identical, unlike removing it — a single walk costs **9%** of deserialization on REGULAR and **20%** on ATTRS, and all the lookups together cost **64%** on ATTRS.

The first slice of that is now gone. On a type with **no derived types** an `xsi:type` attribute cannot dispatch anything: it can only repeat the type's own name or be an error, so in real documents it is simply absent — and the lookup walks the whole head to discover that. The generator therefore emits a filtered accessor for such types, which checks `IndexOf("xsi:type")` first. QNames admit no breaks, so "no substring" proves "no such attribute"; a false positive (the substring inside somebody else's value) falls through to the same honest parse, which is why the observable result is unchanged rather than nearly unchanged.

| Document | Before | After |
|---|---:|---:|
| ATTRS (30 nodes × 3 attribute members) | 9206 ns | **7753 ns — ×0.842** |
| REGULAR (control) | 2158 ns | 2206 ns — within drift |
| WIDE (control) | 3238 ns | 3322 ns — within drift |

Medians of six runs per build; the ATTRS ranges do not overlap (8996–9506 against 7551–7845), the two control ranges overlap almost completely, which is what the mechanism predicts — no head in those documents reaches the filter.

A type **with** derived types keeps the unfiltered accessor on purpose: there the attribute is usually present, the filter would find it, and the parse would then read the head a second time — about 2% on documents that actually use polymorphism. Making the choice in the generator rather than at runtime is what lets both cases win; the axis is not a feature flag but a fact about the type graph, so it is a parameter of `HostFeatureBinding.PreciseNodeTypeAccessor`, not a new `XmlFeature`.

What remains unclaimed is the larger half: the `[XmlAttribute]` member lookups still walk the head once per member.

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
<BaseInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:type="Derived1Info">
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
- **Strings (generated default):** the host calls `inj.ParseBody`, and `DefaultInjector` decodes with `XmlTextDecoder.DecodeElementText` — predefined entities and CharRef, no CDATA. CDATA requires `[XmlFeatures(XmlFeature.CData)]`, and that flag is the one case where the generated host decodes a string itself (`DecodeElementTextWithCData`) instead of going through the injector: `IInjector` has no CDATA-aware overload. A custom injector that rewrites strings therefore keeps working everywhere except on a `CData` host.
- **Strings (`DefaultInjector` / `XmlNode2`):** the public `XmlNode2` walker still understands CDATA; that is not the generated POCO hot path.
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

`XmlNode2` in `XmlSerDe.Common` is a `ref struct` that walks XML without allocating DOM nodes. It remains a **full** XML 1.0 subset (comments, CDATA, quoted attributes, flexible `xsi` prefix) for callers that construct it directly. Generated `[XmlSubject]` deserializers do **not** go through `XmlNode2` and do **not** build `XmlDeserializeSettings` from document-wide comment/CDATA heuristics.

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
- `AppendEncoded(string? value)` — validates then HTML-encodes then appends (used for `string` builtins in element text).
- `AppendEncodedUnchecked(string? value)` — the same without the `XmlCharGuard` check. This is what a host without `[XmlFeatures(XmlFeature.CharGuard)]` calls; it still escapes straight into the sink, so turning the flag off costs no allocation and keeps `null` writing nothing.
- `AppendAttributeEncoded(string? value)` / `AppendAttributeEncodedUnchecked(string? value)` — the same pair for an attribute value, which needs a wider escape set: see the well-formedness note below.
- `AppendBase64(byte[]? value)` — a `byte[]` as one `base64Binary` token. No escaping at all: the base64 alphabet contains no markup and no whitespace, so the same token serves both element text and attribute values. Length estimators are the only exhausters that do not call the encoder here — they compute the encoded length arithmetically rather than building a string only to measure it.

Null nullable values (including nullable value types like `int?`, `DateTime?`) are skipped entirely on serialize rather than emitting an empty tag.

**Culture-invariant formatting:** numeric and `DateTime` values are formatted via `ISpanFormattable.TryFormat` into a stack buffer with `CultureInfo.InvariantCulture` (falling back to an invariant-culture `ToString` for larger values), so output matches XSD's fixed lexical space regardless of the ambient thread culture. `Guid` uses the same stack-buffer `TryFormat` path — note that `StringBuilder` has no `Append(Guid)` overload, so appending one directly would silently bind to `Append(object?)` and box.

**Attribute values escape more than text:** in element text a tab, CR or LF is an ordinary character, but inside an attribute value a reader is *required* to replace each one with a space (XML 1.0 §3.3.3, attribute-value normalization) — the only way to get one through is a character reference written by the producer. So `AppendAttributeEncoded` (`XmlAttributeEncoder`) escapes `< > & "` plus CR, LF and TAB, and leaves the apostrophe alone since the value is always double-quoted — character for character what `XmlWriter` does. It returns the original string instance when there is nothing to escape, which is the overwhelmingly common case.

**Well-formedness guard:** `AppendEncoded` calls `XmlCharGuard.EnsureValidXmlChars` before encoding; `AppendEncodedUnchecked` does not, and it is the one a host without `XmlFeature.CharGuard` calls. `WebUtility.HtmlEncode` escapes `<`, `>`, `&`, `"`, `'` but doesn't know about XML's `Char` production (XML 1.0 §2.2), which forbids most C0 control characters, unpaired surrogates, and `U+FFFE`/`U+FFFF` outright — there is no legal escape for these in XML. String content containing them throws `ArgumentException` instead of silently producing not-well-formed output. Legal whitespace (tab/CR/LF) is allowed through.

### Built-in exhausters

| Exhauster | Purpose |
|-----------|---------|
| `StringBuilderExhauster` | Writes to a `StringBuilder`. Optional pre-sized constructor. Default `DateTime` format: `yyyy-MM-ddTHH:mm:ss.FFFFFFFK`. Not thread-safe. The unbounded string path: a single growing `char[]` would discard the previous array on every doubling, so this stays on `StringBuilder` chunks. |
| `PooledCharExhauster` | Estimated string path. Rents one `char[]` of the given capacity, writes into it, and `ToString()` copies only the written prefix. `Dispose` returns the array to a pool that can hold HUGE buffers (`ArrayPool.Shared` does not). Grows if the estimate undershoots. Not thread-safe. |
| `LengthEstimatorExhauster` | Length estimator: log2 digit widths for integers and exact lexical width for `DateTime` / `TimeSpan` / `decimal`. Sizes `PooledCharExhauster` before the write pass. Exposes `EstimatedTotalLength`. Not thread-safe. |
| `Utf8BinaryExhauster` (abstract) | Converts values to UTF-8 bytes. Small values use an internal buffer; larger values rent from `ArrayPool<byte>`. Subclass and implement `Write(byte[] data, int length)` to send data to a stream or network. |

### Two-phase serialization (length estimation)

```csharp
var estimator = new LengthEstimatorExhauster();
OrderSerializer.Serialize(estimator, order, false);
var estimatedLength = estimator.EstimatedTotalLength;

using var exhauster = new PooledCharExhauster(estimatedLength);
OrderSerializer.Serialize(exhauster, order, false);
string xml = exhauster.ToString();
```

The extra walk of the object graph is cheaper than letting `StringBuilder` double, and unlike a pre-sized `StringBuilder` it does not keep a second UTF-16 copy after `ToString()`. Leave `StringBuilderExhauster` for the case when there is no estimate: unbounded growth of one `char[]` would throw away the previous array on every doubling.

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
| `float` / `float?` | `float` |
| `double` / `double?` | `double` |
| `char` / `char?` | `char` |
| `TimeSpan` / `TimeSpan?` | `duration` |
| `string` | `string` (HTML-encoded on serialize) |

Three of these have a lexical form that does not follow from the type, and each matches what `System.Xml.Serialization` writes:

- **`float` / `double`** use the shortest round-trippable representation, but infinities are written `INF` / `-INF`, not `Infinity`.
- **`char` is written as its code point**, not as the character: `'A'` becomes `65`. XSD has no type for a single character, and the BCL uses one of its own from `http://microsoft.com/wsdl/types/`.
- **`TimeSpan`** is an ISO-8601 duration (`P1DT2H3M4.005S`, `PT0S` for zero, leading minus when negative). Note that `XmlSerializer` only gained `TimeSpan` support in .NET Core — on .NET Framework it writes an empty element and loses the value, so XmlSerDe's output is not byte-compatible with it there. XmlSerDe writes the duration on every target.

`byte[]` is not in the table because it is not decided by type alone. It is written as a single `base64Binary` token — `<Bytes>AQL6</Bytes>`, not one element per byte — but only when the member carries no explicit `[XmlArray]` / `[XmlArrayItem]` wrapper; with one, the array goes back to being an ordinary collection. `List<byte>` is never base64: it stays a collection of `<unsignedByte>` elements. Both rules are `System.Xml.Serialization`'s, measured rather than assumed. An empty array is an empty element, a `null` array is no element at all, and both an empty and a self-closed element read back as `byte[0]`, never `null`.

### Complex types

- Classes registered with `[XmlSubject]`
- **Enums** — serialized as `<EnumTypeName>value</EnumTypeName>`. The generator knows every declared member at compile time, so it emits a `switch` over them on serialize and a chain of span `SequenceEqual` comparisons on deserialize, rather than `Enum.ToString()` / `Enum.Parse` — both of which go through reflection, and `Enum.Parse` additionally boxes its `object` return on every call. `Enum.ToString()` / `Enum.Parse` remain as the fallback arm for values that match no declared member (undefined numeric values, `[Flags]` combinations), so behavior is unchanged for those.
- **Inheritance** — via `[XmlInclude]` and `xsi:type`
- **Collections** — `List<T>` and `T[]` only. `byte[]` is the one array that is not written element-per-item — see the note under [Builtin primitives](#builtin-primitives). A collection *of* `byte[]` (`byte[][]`, `List<byte[]>`), which `System.Xml.Serialization` writes as one `<base64Binary>` element per inner array, is not supported.

### Members

- Public fields and properties (including inherited) with accessible setters. A `List<T>` property without a setter is the one exception: it is filled through `Add` on the instance the constructor created (and skipped entirely if it created none) — matching `System.Xml.Serialization`, which likewise skips a setter-less array, string, or complex type
- `[XmlIgnore]` members (fields as well as properties) are skipped
- `[DefaultValue(x)]` members are omitted when equal to `x`. Write side only — `System.Xml.Serialization` does not restore the default on read either, and doing so here would diverge from it
- The `XxxSpecified` companion pattern is honored: a public `bool` named after the member plus `Specified` gates whether the member is written, and is set to `true` on read as soon as the element is seen
- Private and protected members are skipped
- XML names default to the C# type and member names, and are overridden by the `System.Xml.Serialization` naming attributes: `[XmlRoot]`, `[XmlType]`, `[XmlElement]`, `[XmlArray]`, `[XmlArrayItem]`, `[XmlEnum]`. `[XmlElement(Order = n)]` / `[XmlArray(Order = n)]` set the element order on write.
- `[XmlAttribute]` moves a member into the owner's start tag (`<Owner id="7">`); `[XmlText]` makes it the owner's body, with no tag of its own. Both accept builtin primitives, enums and `byte[]` only — everything else has no plain lexical form, and `System.Xml.Serialization` refuses the same cases (including `Nullable<T>`) for the same reason. A `null` string attribute is not written at all, and an empty body leaves an `[XmlText]` member `null` — both matching the BCL. Attribute values get their own escaping: on top of the markup characters, a literal CR, LF or TAB is written as a character reference (`&#xD;` `&#xA;` `&#x9;`), because a reader is required to replace each of them with a space otherwise (XML 1.0 §3.3.3) — so a string with a newline in an attribute round-trips.

```csharp
public class Message
{
    [XmlAttribute("id")]
    public int Id { get; set; }        // <Message id="7">

    [XmlText]
    public string Body { get; set; }   // <Message id="7">body</Message>
}
```

## Limitations

- **No CDATA serialization** — string content is always emitted entity-escaped, never wrapped in `<![CDATA[...]]>`. Deserialization of CDATA is **opt-in** (`[XmlFeatures(XmlFeature.CData)]`); the default native path treats a literal `<` in element text as an error.
- **No well-formedness *validation* of input on the default path.** By default the deserializer reads what it needs and skips the rest, so a malformed document is often accepted rather than rejected: an unbalanced `<A><B></A>` is caught only where a closing tag is actually checked, unknown elements are skipped, and a duplicated attribute silently resolves to the first one. That leniency is now **opt-in to give up**: `[XmlGuards(XmlGuard.…)]` turns the checks on, and `XmlSerDe.Compat` turns all of them on by itself — see [Opt-in XML guards](#opt-in-xml-guards). Three things stay unchecked even with the full set, and they are named there: closing tags *inside a skipped foreign subtree*, a duplicate attribute reached through two different prefixes bound to the same namespace, and the line/position in the exception.

  What the default path will *not* do is fail with a bounds error: parsing a span is index arithmetic, and an `IndexOutOfRangeException` there is a statement about the parser, not about the document. Every malformed input ends either in a result or in an `InvalidOperationException` (`XmlDocumentException` derives from it) or a `FormatException` when a lexeme is in place but is not a number. That invariant is held by a fuzz corpus built mechanically from valid documents — every prefix, every single-character deletion, every single-character replacement with a markup character — plus hand-written broken heads and unterminated comment/CDATA/PI/DOCTYPE markup; a 20 000-deep unknown element is included too, since skipping a foreign subtree counts tags rather than recursing. (Serialization *output* may run `XmlCharGuard` when `[XmlFeatures(XmlFeature.CharGuard)]` is set: illegal XML 1.0 `Char` then throws `ArgumentException`. Default native serialize does not.)
- **Parameterless constructor required** unless `[XmlFactory]` is used.
- Serialized types must be visible to the serializer partial class.
- Members need accessible setters for deserialization, except setter-less `List<T>` properties (see [Members](#members)).
- **Only `List<T>` and `T[]`** as collections.
- The serializer class must be `partial`.
- Unknown member types cause a compile-time generator error.
- Multi-argument generics (e.g. `Dictionary<K,V>`) are not supported.

See also [Out of scope by design](#out-of-scope-by-design) for XML 1.0 features that aren't limitations to be lifted later, but deliberate consequences of targeting the POCO ↔ XML data-binding scenario.

## Out of scope by design

XmlSerDe targets POCO ↔ XML data binding, not general-purpose XML processing. The following XML 1.0 / XML Namespaces features are consequences of that scope, not oversights — each is unlikely to matter for typical data-transfer XML (including everything `System.Xml.Serialization` itself produces for the primitives, collections, and polymorphism XmlSerDe supports), but matters for interop with documents from other kinds of XML producers.

- **No DTD support.** A `<!DOCTYPE ...>` is a parse error on the default native path. With `[XmlFeatures(XmlFeature.Markup)]` (or `CutXmlHead(true, span)`) it is skipped, not parsed — so any custom general entities it declares are not resolved. Only the five predefined XML entities (`&amp;`, `&lt;`, `&gt;`, `&apos;`, `&quot;`) plus character references (`&#49;`, `&#x31;`) are understood; a reference to anything else — including an HTML named entity such as `&nbsp;` — throws, because without a DTD declaring it that is a well-formedness error (XML 1.0 §4.1, WFC: Entity Declared) rather than text to pass through. There's also no DTD-based content validation and no fetching of external DTDs.
- **No general XML Namespaces support.** Only one namespace is special-cased: `http://www.w3.org/2001/XMLSchema-instance` for `xsi:type` / `xsi:nil`. Default native reads the **literal** prefix `xsi`. A different prefix (`xmlns:p3=… p3:type=…`) needs `[XmlFeatures(XmlFeature.FlexibleXsiPrefix)]`. Beyond that, element and attribute names are compared as literal text, prefix included; there's no general prefix-to-URI resolution or default-namespace (`xmlns="uri"`) handling.
- **`xml:space`, `xml:lang`, `xml:base` are not interpreted.** In practice this rarely matters for `xml:space`: text content is always preserved verbatim regardless (matching the XML default, `xml:space="preserve"`) — but `xml:space="default"`, which would opt back into whitespace collapsing, has no effect either.
- **No mixed content.** An element is parsed as either plain text or a list of child elements, never an interleaving of both — text appearing between child elements is discarded rather than bound to any member. Consequently a type that combines an `[XmlText]` member with element members — which `System.Xml.Serialization` does support — fails to build, rather than silently producing a document with the text missing.
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

**Incrementality.** The usual advice for incremental generators — never put a `Compilation` in the pipeline, drive generation from the annotated declaration alone — does not apply here unchanged. `[XmlSubject(typeof(T))]` sits on the host class, but the emitted code is derived from the *transitive type graph* rooted at `T`, and those types live in other files. Since Roslyn re-runs a syntax provider's transform only for trees that changed, a generator keyed on the host's syntax node would be perfectly incremental and would happily serve stale code after a member is renamed elsewhere.

So the `Compilation` stays an input, and symbol binding runs on every edit. Caching comes from *output equality* instead: the pipeline's last step yields plain strings and diagnostic descriptions with no symbols, syntax nodes, or compilations in them, so whenever an edit doesn't change the generated text, the source-output step is reported as `Cached` and Roslyn reuses the already-parsed generated trees rather than re-parsing and re-binding them. That reuse, not the generator's own work, is the dominant cost in the IDE. Both halves of the contract — cache hits on irrelevant edits, cache *misses* on cross-file changes that matter — are covered by `GeneratorIncrementalityFixture`.

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
| `XmlObject1_*` | Empty root, self-closing tags, `xsi:type`, stream serialization; comment documents use `XmlSerializerDeserializerComments` |
| `XmlObject2_*` | Primitives, XML entity decoding, XML declaration stripping; comment documents use the Comments host |
| `XmlObject4_5_*` | Abstract polymorphism with `xsi:type` |
| `XmlFeatureFixture` | Per-flag matrix from `docs/opt-in-xml-features.md`: default rejects comments/CDATA/PI/DOCTYPE/`p3:type`/single quotes; each flag accepts its document and rejects a neighbour |
| `XmlFeatureGeneratorFixture` / `HostFeatureBindingFixture` | Generated text has no document-wide heuristics on default hosts; flags → primitive names without Roslyn; two hosts in one compilation stay distinct; compat injects the full set without a user `[XmlFeatures]` |
| `XmlGuardFixture` | Per-guard matrix from `docs/opt-in-xml-guards.md`: the same document goes to a host without the attribute (which must **accept** it) and to a host with exactly one flag (which must refuse it, and only on its own violation). Includes the traps a guard must not fall into - `&lt;Foo/&gt;`, an empty body, an empty collection, a skipped foreign subtree, a head with ten distinct attributes |
| `XsiTypePrefilterFixture` | Both edges of the `IndexOf("xsi:type")` negative filter that a type without derived types is compiled with: a foreign `xsi:type` still throws and one naming the type itself is still accepted (the filter may not swallow a real attribute), while the substring sitting inside somebody else's attribute value stays harmless (a false positive may not become a parse error). Both variants covered — literal `xsi` and the runtime prefix of `FlexibleXsiPrefix` |
| `XmlGuardGeneratorFixture` / `HostGuardBindingFixture` | A default host mentions no guard primitive and carries no extra parameter; `XmlGuard.None` is textually identical to no attribute; one flag emits only its own primitive; flags → snippets without Roslyn; compat gets the full set with no user `[XmlGuards]` and does not change the native host next to it |
| `AttributeSyntaxFixture` | `Attribute ::= Name Eq AttValue` on both host families: legal whitespace around `=` is read (it used to be dropped), a missing quote or `=` throws instead of stealing the neighbour&#39;s value |
| `Compat/CompatGuardFixture` | The facade against the BCL on the malformed-document matrix: outer type and message (without the position), inner `XmlException`, expectation taken from the BCL inside the test. Plus Misc after the root, which must **not** start failing, and a native host in the same assembly that still accepts a foreign closing tag |
| `GeneratorIncrementalityFixture` | Cache hits on comments in the host file; cache misses on `[XmlFeatures]` / `[XmlGuards]` add/remove and on member rename in another file |
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
| `SinglePassParserFixture` | Consequences of single-pass deserialization: unknown elements skipped by tag balance (plain skip and `>` inside attribute values on the default host; CDATA-in-unknown on the CData host), self-closing children not ending the sibling loop, mismatched/truncated closing tags rejected, compact vs. indented parity |
| `XmlTextDecoderFixture` | Reference expansion per XML 1.0 §4.1: the five predefined entities, decimal/hex character references incl. above-BMP surrogate pairs, CDATA in any position, attribute-value normalization vs. references, and every reference form XML rejects (undeclared, unterminated, empty, uppercase `X`, illegal or out-of-range code point) |
| `PooledArrayBuilderFixture` | Array members across the pooled builder's growth steps (every other array test uses 3 elements and never reaches one), for primitive, struct and reference element types; empty-array and builder-reuse semantics |
| `SerDeFixtureV2` | Same feature set as `SerDeFixture`, exercised through a second serializer declaration to catch cross-class code-gen issues |
| `CoverageExpansionFixture` | All primitive types incl. `decimal`/`Guid` round-trips, nullable value-type omission on serialize, `LengthEstimatorExhauster` upper-bound accuracy, empty/null collections, CDATA strings (including concatenated blocks), HTML-entity-encoded string serialization |
| `RegularAccEstMemoryFixture` | `LengthEstimatorExhauster` never undershoots REGULAR, including apostrophe → `&#39;` overhead that a tight estimate must count |
| `SpecComplianceFixture` | XML 1.0 edge cases: unescaped `>` in attribute values (default host), prolog PI / DOCTYPE skipped when `Markup` (or `CutXmlHead(true, …)`) is on, `XmlNode2` attribute-value whitespace normalization vs. character references |
| `MalformedInputFixture` | Malformed input never produces a bounds error: a corpus built mechanically from valid documents (every prefix, every single-character deletion, every single-character replacement with a markup character) plus broken attribute heads, unterminated comment/CDATA/PI/DOCTYPE markup, and a 20 000-deep unknown element. Every outcome must be a result, an `InvalidOperationException` or a `FormatException` |
| `Interop/*` | Differential harness: 36 POCO shapes run through both XmlSerDe and `System.Xml.Serialization` in all three directions (each reads the other's output; the two documents are compared). Divergences are pinned by tests as well, so closing one turns a test red on purpose. A compatibility table is written to `interop-report.md` next to the test assembly |
| `Interop/BinaryLexicalFixture` | `base64Binary` lexical edges that neither side writes and the differential runs therefore cannot produce: empty and self-closing elements decoding to `byte[0]` rather than `null`, whitespace inside the lexeme, a corrupt lexeme. Each is asserted as "both sides agree", not as "ours works" |
| `Compat/CompatFixture` | The facade at runtime: it *is* a `System.Xml.Serialization.XmlSerializer`, a refused type still round-trips through the fallback, a transitive graph is accelerated from a single call site, every overload — including calls through the base-typed reference — produces a document the BCL can read, and a failure surfaces as the same exception the BCL raises for it (the expectation is taken from the BCL inside the test, not written down as a literal). Also the paths that must *not* be accelerated: a subscriber on the deserialization events (whether attached to the instance or passed into the call), a non-null `encodingStyle`, and a non-empty extra constructor argument — plus `FromTypes` and the factory, whose types are named nowhere else, so the test fails if the generator stops seeing those call sites. Opt-in XML (CDATA, comments, `p3:type`, quotes, `>` in attributes) is accepted **without** a user `[XmlFeatures]`; a native default host in the same process still rejects CDATA |
| `Compat/CompatGeneratorFixture` | The facade at build time, driven through `CSharpGeneratorDriver`: which types are refused and why (including attributes that would otherwise be ignored silently, and the controls proving the refusals didn't spread — a plain enum and six ordinary naming attributes stay accelerated), which call-site shapes are recognized (`new`, `FromTypes`, the facade's own factory — but not the BCL's, and not an array in a variable), that a refusal of one root leaves the others accelerated, that `XmlSerDeCompatStrict` changes severity only, and that a project without `XmlSerDe.Compat` gets no generated code at all |

## Alternatives

You may also be interested in [StackXML](https://github.com/ZingBallyhoo/StackXML).
