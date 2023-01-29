using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.PerformanceTests
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser]
    public class OtherStuffFixture
    {


    }

}