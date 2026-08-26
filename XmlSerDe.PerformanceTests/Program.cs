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
            new DeserializeMatrixFixture();
            new SerializeMatrixFixture();
#else

            BenchmarkDotNet.Running.BenchmarkRunner.Run(
                new[]
                {
                    typeof(DeserializeMatrixFixture),
                    typeof(SerializeMatrixFixture)
                });
#endif
        }
    }

}
