using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System;
using System.Net;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Deep;
using XmlSerDe.Tests.Huge;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep.Subject;
using XmlSerDe.Tests.Huge.Subject;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Deserialize matrix: (4.7.2 + 8 + 10) * (regular + huge + deep)
/// </summary>
[SimpleJob(RuntimeMoniker.Net472)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[DeserializeReportColumns]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DeserializeMatrixFixture
{
    private const string RegularCategory = "REGULAR";
    private const string RegularCompatibleCategory = "REGULAR_COMPAT";
    private const string HugeCategory = "HUGE";
    private const string DeepCategory = "DEEP";

    [BenchmarkCategory(RegularCategory)]
    [Benchmark(Description = "Deserialize: REGULAR: System.Xml", Baseline = true)]
    public InfoContainer Deserialize_Regular_SystemXml()
    {
        return ComplexFixture.Deserialize_SystemXml(ComplexFixture.AuxXml);
    }

    [BenchmarkCategory(HugeCategory)]
    [Benchmark(Description = "Deserialize: HUGE: System.Xml", Baseline = true)]
    public HugeDocument Deserialize_Huge_SystemXml()
    {
        return HugeFixture.Deserialize_SystemXml(HugeBenchmarkPayload.Xml);
    }

    [BenchmarkCategory(DeepCategory)]
    [Benchmark(Description = "Deserialize: DEEP: System.Xml", Baseline = true)]
    public DeepNode Deserialize_Deep_SystemXml()
    {
        return DeepFixture.Deserialize_SystemXml(DeepFixture.DeepXml);
    }

    [BenchmarkCategory(RegularCategory)]
    [Benchmark(Description = "Deserialize: REGULAR: XmlSerDe")]
    public InfoContainer Deserialize_Regular_XmlSerDe()
    {
        return ComplexFixture.Deserialize_XmlSerDe(ComplexFixture.AuxXml.AsSpan());
    }

    [BenchmarkCategory(HugeCategory)]
    [Benchmark(Description = "Deserialize: HUGE: XmlSerDe")]
    public HugeDocument Deserialize_Huge_XmlSerDe()
    {
        return HugeFixture.Deserialize_XmlSerDe(HugeBenchmarkPayload.Xml.AsSpan());
    }

    [BenchmarkCategory(DeepCategory)]
    [Benchmark(Description = "Deserialize: DEEP: XmlSerDe")]
    public DeepNode Deserialize_Deep_XmlSerDe()
    {
        return DeepFixture.Deserialize_XmlSerDe(DeepFixture.DeepXml.AsSpan());
    }

    [BenchmarkCategory(RegularCompatibleCategory)]
    [Benchmark(Description = "Deserialize: REGULAR: XmlSerDe SystemXmlCompatible")]
    public InfoContainer Deserialize_Regular_XmlSerDe_Compatible()
    {
        return ComplexFixture.Deserialize_XmlSerDe_Compatible(ComplexFixture.AuxXml.AsSpan());
    }
}

