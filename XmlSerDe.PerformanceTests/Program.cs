using BenchmarkDotNet.Exporters;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftAntimalwareEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using XmlSerDe.Generator.EmbeddedCode;
using XmlSerDe.PerformanceTests.Subject;

namespace XmlSerDe.PerformanceTests
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG

#else
            BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(DeserializeFixture));
            //BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(XmlDecodeStringFixture));

            //var span = DeserializeFixture.AuxXml.AsSpan();
            //for (var cc = 0; cc < 250000; cc++)
            //{
            //    XmlSerializerDeserializer.Deserialize(span, out InfoContainer r);
            //}
#endif
        }
    }

}
