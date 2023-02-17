using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;

namespace XmlSerDe.PerformanceTests;


/*

|   Method | Size |       Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|--------- |----- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Allocate |  256 |   161.0 ns |  1.39 ns |  1.16 ns |  1.00 |    0.00 | 0.0668 |     280 B |        1.00 |
|     Rent |  256 |   163.6 ns |  3.15 ns |  2.80 ns |  1.01 |    0.02 |      - |         - |        0.00 |
|          |      |            |          |          |       |         |        |           |             |
| Allocate | 1024 |   618.6 ns | 11.32 ns | 15.50 ns |  1.00 |    0.00 | 0.2499 |    1048 B |        1.00 |
|     Rent | 1024 |   595.2 ns |  9.20 ns |  8.16 ns |  0.96 |    0.02 |      - |         - |        0.00 |
|          |      |            |          |          |       |         |        |           |             |
| Allocate | 4096 | 2,419.5 ns | 34.68 ns | 34.06 ns |  1.00 |    0.00 | 0.9842 |    4120 B |        1.00 |
|     Rent | 4096 | 2,249.9 ns | 44.14 ns | 47.23 ns |  0.93 |    0.02 |      - |         - |        0.00 
 
*/





[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
//[Config(typeof(DontForceGcCollectionsConfig))] // we don't want to interfere with GC, we want to include it's impact
public class OtherFixture
{
    [Params((int)256, (int)1024, (int)4096)]
    public int Size { get; set; }

    [Benchmark(Baseline = true)]
    public int Allocate()
    {
        var buffer = new byte[Size];
        return Sum(buffer);
    }

    [Benchmark()]
    public int Rent()
    {
        int sum = 0;
        byte[]? buffer = default;
        try
        {
            buffer = ArrayPool<byte>.Shared.Rent(Size);
            sum = Sum(buffer.AsSpan(0, Size));
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Sum(ReadOnlySpan<byte> span)
    {
        var sum = 0;
        
        for(var i = 0; i < span.Length; i++) 
        {
            sum += span[i];
        }

        return sum;
    }
}

//public class DontForceGcCollectionsConfig : ManualConfig
//{
//    public DontForceGcCollectionsConfig()
//    {
//        AddJob(Job.Default
//            .WithGcMode(
//                new GcMode()
//                {
//                    Force = false // tell BenchmarkDotNet not to force GC collections after every iteration
//                }
//                )
//            );
//    }
//}
