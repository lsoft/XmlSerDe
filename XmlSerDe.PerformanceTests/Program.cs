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
            if (args.Length > 0 && string.Equals(args[0], "--feature-cost", StringComparison.Ordinal))
            {
                FeatureCostProbe.Run();
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--guard-cost", StringComparison.Ordinal))
            {
                GuardCostProbe.Run();
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--attr-cost", StringComparison.Ordinal))
            {
                AttributeCostProbe.Run();
                return;
            }

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
