using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests;
using XmlSerDe.Tests.Complex.Subject;

namespace XmlSerDe.PerformanceTests;

/*

Воспроизводит конкретные точки лишних аллокаций, найденные при анализе
XmlSerDe.Components / XmlSerDe.Generator (см. обсуждение):

1) DefaultStringBuilderExhauster.Append(Guid) и Append(Guid?) вызывают
   StringBuilder.Append(value), а у StringBuilder нет перегрузки Append(Guid) -
   резолвится Append(object?), то есть Guid боксится в кучу, а внутри ещё раз
   вызывается value.ToString() (вторая аллокация). Соседние Append(DateTime)/
   Append(int) и т.д. уже используют TryFormat в stackalloc-буфер без аллокаций.

2) Utf8BinaryExhauster.Append(Guid) явно делает value.ToString() (см. TODO в
   самом файле) вместо TryFormat в stackalloc-буфер.

3) Генератор эмитит для enum-полей:
   - сериализация: obj.Prop.ToString() - Enum.ToString() идёт через reflection
     и всегда аллоцирует новую строку (в отличие от строковых литералов);
   - десериализация: (T)Enum.Parse(typeof(T), span) - Enum.Parse возвращает
     object, то есть результат боксится в куче на каждый вызов.

Ничего в самой библиотеке этот файл не меняет - только измеряет "как есть",
в нескольких вариантах (x1 - разовый вызов, xN - несколько значений подряд,
чтобы GC0/Allocated не терялись в шуме и было видно линейный рост), и на
нескольких типах enum, встречающихся в этом же решении.

*/

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class AllocationHotspotsFixture
{
    private static readonly Guid[] Guids = new[]
    {
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        Guid.Parse("77777777-7777-7777-7777-777777777777"),
        Guid.Parse("88888888-8888-8888-8888-888888888888"),
    };

    private static readonly KeyValueKindEnum[] KeyValueKinds = new[]
    {
        KeyValueKindEnum.Zero,
        KeyValueKindEnum.One,
        KeyValueKindEnum.Two,
        KeyValueKindEnum.Three,
    };

    private static readonly string[] KeyValueKindNames = new[] { "Zero", "One", "Two", "Three" };

    private static readonly InfoTypeEnum[] InfoTypes = new[]
    {
        InfoTypeEnum.Derived1,
        InfoTypeEnum.Derived2,
        InfoTypeEnum.Derived3,
    };

    private static readonly string[] InfoTypeNames = new[] { "Derived1", "Derived2", "Derived3" };

    private static readonly XmlEnum15[] XmlEnum15Values = new[]
    {
        XmlEnum15.EnumValue0,
        XmlEnum15.EnumValue1,
    };

    private static readonly string[] XmlEnum15Names = new[] { "EnumValue0", "EnumValue1" };

    #region Guid append: DefaultStringBuilderExhauster (StringBuilder.Append(object) boxing)

    [Benchmark(Description = "Guid x1: DefaultStringBuilderExhauster.Append")]
    public string GuidAppend_StringBuilder_x1()
    {
        var exh = new DefaultStringBuilderExhauster();
        exh.Append(Guids[0]);
        return exh.ToString();
    }

    [Benchmark(Description = "Guid x8: DefaultStringBuilderExhauster.Append")]
    public string GuidAppend_StringBuilder_x8()
    {
        var exh = new DefaultStringBuilderExhauster();
        for (var i = 0; i < Guids.Length; i++)
        {
            exh.Append(Guids[i]);
        }
        return exh.ToString();
    }

    #endregion

    #region Guid append: Utf8BinaryExhauster (explicit value.ToString() fallback, see TODO in source)

    [Benchmark(Description = "Guid x1: Utf8BinaryExhauster.Append")]
    public void GuidAppend_Utf8Binary_x1()
    {
        var exh = new Utf8BinaryExhausterEmpty();
        exh.Append(Guids[0]);
    }

    [Benchmark(Description = "Guid x8: Utf8BinaryExhauster.Append")]
    public void GuidAppend_Utf8Binary_x8()
    {
        var exh = new Utf8BinaryExhausterEmpty();
        for (var i = 0; i < Guids.Length; i++)
        {
            exh.Append(Guids[i]);
        }
    }

    #endregion

    #region Enum serialize: Enum.ToString() reflection path, exactly as emitted by ClassSourceProducer.GenerateSerializeEnum

    [Benchmark(Description = "Enum x1: KeyValueKindEnum.ToString()")]
    public string EnumToString_KeyValueKind_x1()
    {
        return KeyValueKinds[0].ToString();
    }

    [Benchmark(Description = "Enum x4: KeyValueKindEnum.ToString()")]
    public string EnumToString_KeyValueKind_x4()
    {
        string? last = null;
        for (var i = 0; i < KeyValueKinds.Length; i++)
        {
            last = KeyValueKinds[i].ToString();
        }
        return last!;
    }

    [Benchmark(Description = "Enum x1: InfoTypeEnum.ToString()")]
    public string EnumToString_InfoType_x1()
    {
        return InfoTypes[0].ToString();
    }

    [Benchmark(Description = "Enum x3: InfoTypeEnum.ToString()")]
    public string EnumToString_InfoType_x3()
    {
        string? last = null;
        for (var i = 0; i < InfoTypes.Length; i++)
        {
            last = InfoTypes[i].ToString();
        }
        return last!;
    }

    [Benchmark(Description = "Enum x1: XmlEnum15.ToString()")]
    public string EnumToString_XmlEnum15_x1()
    {
        return XmlEnum15Values[0].ToString();
    }

    #endregion

    #region Enum deserialize: Enum.Parse(Type, ReadOnlySpan<char>) boxing path, exactly as emitted by ClassSourceProducer.GenerateEnumParseStatement

    [Benchmark(Description = "Enum x1: Enum.Parse(KeyValueKindEnum)")]
    public KeyValueKindEnum EnumParse_KeyValueKind_x1()
    {
        return (KeyValueKindEnum)Enum.Parse(typeof(KeyValueKindEnum), KeyValueKindNames[0].AsSpan());
    }

    [Benchmark(Description = "Enum x4: Enum.Parse(KeyValueKindEnum)")]
    public KeyValueKindEnum EnumParse_KeyValueKind_x4()
    {
        KeyValueKindEnum last = default;
        for (var i = 0; i < KeyValueKindNames.Length; i++)
        {
            last = (KeyValueKindEnum)Enum.Parse(typeof(KeyValueKindEnum), KeyValueKindNames[i].AsSpan());
        }
        return last;
    }

    [Benchmark(Description = "Enum x1: Enum.Parse(InfoTypeEnum)")]
    public InfoTypeEnum EnumParse_InfoType_x1()
    {
        return (InfoTypeEnum)Enum.Parse(typeof(InfoTypeEnum), InfoTypeNames[0].AsSpan());
    }

    [Benchmark(Description = "Enum x3: Enum.Parse(InfoTypeEnum)")]
    public InfoTypeEnum EnumParse_InfoType_x3()
    {
        InfoTypeEnum last = default;
        for (var i = 0; i < InfoTypeNames.Length; i++)
        {
            last = (InfoTypeEnum)Enum.Parse(typeof(InfoTypeEnum), InfoTypeNames[i].AsSpan());
        }
        return last;
    }

    [Benchmark(Description = "Enum x1: Enum.Parse(XmlEnum15)")]
    public XmlEnum15 EnumParse_XmlEnum15_x1()
    {
        return (XmlEnum15)Enum.Parse(typeof(XmlEnum15), XmlEnum15Names[0].AsSpan());
    }

    #endregion
}
