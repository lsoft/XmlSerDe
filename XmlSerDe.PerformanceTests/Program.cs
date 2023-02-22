using BenchmarkDotNet.Exporters;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace XmlSerDe.PerformanceTests
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            new SerializeDeserializeFixture();
#else

            BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(SerializeDeserializeFixture));
            //BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(OtherFixture));
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
