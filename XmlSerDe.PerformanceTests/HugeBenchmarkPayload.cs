using System;
using XmlSerDe.Tests.Huge;
using XmlSerDe.Tests.Huge.Subject;

namespace XmlSerDe.PerformanceTests;

/*
Shared 100 MB-ish HUGE corpus for all benchmark runs.

It's intentionally built once per process (static ctor) so that
benchmarked methods only measure the serializer/deserializer work.
*/
internal static class HugeBenchmarkPayload
{
    internal static readonly HugeDocument Object;
    internal static readonly string Xml;

    static HugeBenchmarkPayload()
    {
        Object = HugeDocumentBuilder.BuildForTargetLength(
            HugeDocumentBuilder.TargetXmlLength,
            HugeFixture.Serialize_XmlSerDe
        );

        Xml = HugeFixture.Serialize_XmlSerDe(Object);

        var min = (int)(HugeDocumentBuilder.TargetXmlLength * 0.90);
        var max = (int)(HugeDocumentBuilder.TargetXmlLength * 1.15);
        if (Xml.Length < min || Xml.Length > max)
        {
            throw new InvalidOperationException(
                "HUGE XML length " + Xml.Length + " is not ~100 MB (records: "
                + (Object.Records == null ? 0 : Object.Records.Count) + ")."
            );
        }
    }
}

