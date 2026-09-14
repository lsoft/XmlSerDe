using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using XmlSerDe;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep;
using XmlSerDe.Tests.Huge;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Serialize matrix. Method names are System.Xml / XmlSerDe; shape, exhauster and
/// preestimate are report columns:
/// stringbuilder — StringWriter / StringBuilderExhauster;
/// pooledchar — PooledCharExhauster after LengthEstimatorExhauster;
/// emptystream — Utf8BinaryExhausterEmpty (UTF-8 encode, no I/O);
/// memorystream — MemoryStream UTF-8 / Utf8BinaryExhausterStream, empty or pre-sized.
/// Memorystream keeps its own grouping key so it is not ratio'd against StringWriter.
/// </summary>
[SimpleJob(RuntimeMoniker.Net472)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[SerializeReportColumns]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SerializeMatrixFixture
{
    private const string RegularCategory = "REGULAR";
    private const string HugeCategory = "HUGE";
    private const string DeepCategory = "DEEP";
    private const string RegularMemoryStreamCategory = RegularCategory + ReportLayout.MemoryStreamGroupSuffix;
    private const string HugeMemoryStreamCategory = HugeCategory + ReportLayout.MemoryStreamGroupSuffix;
    private const string DeepMemoryStreamCategory = DeepCategory + ReportLayout.MemoryStreamGroupSuffix;

    private static int SystemXml_ToString(XmlSerializer serializer, object obj)
    {
        using var writer = new StringWriter();
        serializer.Serialize(writer, obj);
        return Encoding.UTF8.GetByteCount(writer.ToString());
    }

    private static int SystemXml_ToStream(XmlSerializer serializer, object obj)
    {
        using var ms = new MemoryStream();
        serializer.Serialize(ms, obj);
        return (int)ms.Length;
    }

    private static int XmlSerDe_StringBuilder_Regular()
    {
        var exhauster = new StringBuilderExhauster();
        XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    private static int XmlSerDe_StringBuilder_Deep()
    {
        var exhauster = new StringBuilderExhauster();
        DeepXmlSerializerDeserializer.Serialize(exhauster, DeepFixture.DefaultObject, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    private static int XmlSerDe_StringBuilder_Huge()
    {
        var exhauster = new StringBuilderExhauster();
        HugeXmlSerializerDeserializer.Serialize(exhauster, HugeBenchmarkPayload.Object, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    private static int XmlSerDe_MemoryStream_Regular()
    {
        using var ms = new MemoryStream();
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_MemoryStream_Deep()
    {
        using var ms = new MemoryStream();
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            DeepXmlSerializerDeserializer.Serialize(exhauster, DeepFixture.DefaultObject, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_MemoryStream_Huge()
    {
        using var ms = new MemoryStream();
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            HugeXmlSerializerDeserializer.Serialize(exhauster, HugeBenchmarkPayload.Object, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_MemoryStream_Preestimate_Regular()
    {
        var estimator = new LengthEstimatorExhauster();
        XmlSerializerDeserializer.Serialize(estimator, ComplexFixture.DefaultObject, appendXmlHead: false);

        using var ms = new MemoryStream(estimator.EstimatedTotalLength + estimator.EstimatedTotalLength / 10);
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_MemoryStream_Preestimate_Deep()
    {
        var estimator = new LengthEstimatorExhauster();
        DeepXmlSerializerDeserializer.Serialize(estimator, DeepFixture.DefaultObject, appendXmlHead: false);

        using var ms = new MemoryStream(estimator.EstimatedTotalLength + estimator.EstimatedTotalLength / 10);
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            DeepXmlSerializerDeserializer.Serialize(exhauster, DeepFixture.DefaultObject, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_MemoryStream_Preestimate_Huge()
    {
        var estimator = new LengthEstimatorExhauster();
        HugeXmlSerializerDeserializer.Serialize(estimator, HugeBenchmarkPayload.Object, appendXmlHead: false);

        using var ms = new MemoryStream(estimator.EstimatedTotalLength + estimator.EstimatedTotalLength / 10);
        using (var exhauster = new Utf8BinaryExhausterStream(ms))
        {
            HugeXmlSerializerDeserializer.Serialize(exhauster, HugeBenchmarkPayload.Object, appendXmlHead: false);
        }

        return (int)ms.Length;
    }

    private static int XmlSerDe_Empty_Regular()
    {
        var exhauster = new Utf8BinaryExhausterEmpty();
        XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, appendXmlHead: false);
        return exhauster.Written;
    }

    private static int XmlSerDe_Empty_Deep()
    {
        var exhauster = new Utf8BinaryExhausterEmpty();
        DeepXmlSerializerDeserializer.Serialize(exhauster, DeepFixture.DefaultObject, appendXmlHead: false);
        return exhauster.Written;
    }

    private static int XmlSerDe_Empty_Huge()
    {
        var exhauster = new Utf8BinaryExhausterEmpty();
        HugeXmlSerializerDeserializer.Serialize(exhauster, HugeBenchmarkPayload.Object, appendXmlHead: false);
        return exhauster.Written;
    }

    private static int XmlSerDe_PooledChar_Regular()
    {
        var estimator = new LengthEstimatorExhauster();
        XmlSerializerDeserializer.Serialize(estimator, ComplexFixture.DefaultObject, appendXmlHead: false);

        using var exhauster = new PooledCharExhauster(estimator.EstimatedTotalLength);
        XmlSerializerDeserializer.Serialize(exhauster, ComplexFixture.DefaultObject, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    private static int XmlSerDe_PooledChar_Deep()
    {
        var estimator = new LengthEstimatorExhauster();
        DeepXmlSerializerDeserializer.Serialize(estimator, DeepFixture.DefaultObject, appendXmlHead: false);

        using var exhauster = new PooledCharExhauster(estimator.EstimatedTotalLength);
        DeepXmlSerializerDeserializer.Serialize(exhauster, DeepFixture.DefaultObject, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    private static int XmlSerDe_PooledChar_Huge()
    {
        var estimator = new LengthEstimatorExhauster();
        HugeXmlSerializerDeserializer.Serialize(estimator, HugeBenchmarkPayload.Object, appendXmlHead: false);

        using var exhauster = new PooledCharExhauster(estimator.EstimatedTotalLength);
        HugeXmlSerializerDeserializer.Serialize(exhauster, HugeBenchmarkPayload.Object, appendXmlHead: false);
        var xml = exhauster.ToString();
        return Encoding.UTF8.GetByteCount(xml);
    }

    // -------------------- REGULAR (stringbuilder / pooledchar) --------------------

    [BenchmarkCategory(RegularCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Regular_SystemXml()
        => SystemXml_ToString(ComplexFixture.SystemXmlSerializer, ComplexFixture.DefaultObject);

    [BenchmarkCategory(RegularCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Regular_XmlSerDe() => XmlSerDe_StringBuilder_Regular();

    [BenchmarkCategory(RegularCategory)]
    [Sink(Sinks.PooledChar)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Regular_PooledChar() => XmlSerDe_PooledChar_Regular();

    [BenchmarkCategory(RegularCategory)]
    [Sink(Sinks.EmptyStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Regular_Empty() => XmlSerDe_Empty_Regular();

    // -------------------- REGULAR (memorystream) --------------------

    [BenchmarkCategory(RegularMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Regular_SystemXml_MemoryStream()
        => SystemXml_ToStream(ComplexFixture.SystemXmlSerializer, ComplexFixture.DefaultObject);

    [BenchmarkCategory(RegularMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Regular_MemoryStream() => XmlSerDe_MemoryStream_Regular();

    [BenchmarkCategory(RegularMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Regular_MemoryStream_Preestimate() => XmlSerDe_MemoryStream_Preestimate_Regular();

    // -------------------- HUGE (stringbuilder / pooledchar / empty) --------------------

    [BenchmarkCategory(HugeCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Huge_SystemXml()
        => SystemXml_ToString(HugeFixture.SystemXmlSerializer, HugeBenchmarkPayload.Object);

    [BenchmarkCategory(HugeCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Huge_XmlSerDe() => XmlSerDe_StringBuilder_Huge();

    [BenchmarkCategory(HugeCategory)]
    [Sink(Sinks.PooledChar)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Huge_PooledChar() => XmlSerDe_PooledChar_Huge();

    [BenchmarkCategory(HugeCategory)]
    [Sink(Sinks.EmptyStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Huge_Empty() => XmlSerDe_Empty_Huge();

    // -------------------- HUGE (memorystream) --------------------

    [BenchmarkCategory(HugeMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Huge_SystemXml_MemoryStream()
        => SystemXml_ToStream(HugeFixture.SystemXmlSerializer, HugeBenchmarkPayload.Object);

    [BenchmarkCategory(HugeMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Huge_MemoryStream() => XmlSerDe_MemoryStream_Huge();

    [BenchmarkCategory(HugeMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Huge_MemoryStream_Preestimate() => XmlSerDe_MemoryStream_Preestimate_Huge();

    // -------------------- DEEP (stringbuilder / pooledchar / empty) --------------------

    [BenchmarkCategory(DeepCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Deep_SystemXml()
        => SystemXml_ToString(DeepFixture.SystemXmlSerializer, DeepFixture.DefaultObject);

    [BenchmarkCategory(DeepCategory)]
    [Sink(Sinks.StringBuilder)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Deep_XmlSerDe() => XmlSerDe_StringBuilder_Deep();

    [BenchmarkCategory(DeepCategory)]
    [Sink(Sinks.PooledChar)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Deep_PooledChar() => XmlSerDe_PooledChar_Deep();

    [BenchmarkCategory(DeepCategory)]
    [Sink(Sinks.EmptyStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Deep_Empty() => XmlSerDe_Empty_Deep();

    // -------------------- DEEP (memorystream) --------------------

    [BenchmarkCategory(DeepMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "System.Xml", Baseline = true)]
    public int Serialize_Deep_SystemXml_MemoryStream()
        => SystemXml_ToStream(DeepFixture.SystemXmlSerializer, DeepFixture.DefaultObject);

    [BenchmarkCategory(DeepMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Deep_MemoryStream() => XmlSerDe_MemoryStream_Deep();

    [BenchmarkCategory(DeepMemoryStreamCategory)]
    [Sink(Sinks.MemoryStream)]
    [Preestimate]
    [Benchmark(Description = "XmlSerDe")]
    public int Serialize_Deep_MemoryStream_Preestimate() => XmlSerDe_MemoryStream_Preestimate_Deep();
}
