using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using XmlSerDe.Tests;
using XmlSerDe.Tests.Dispatch;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Чего стоит форма наследника, когда контракт стока - базовый класс с
/// virtual-методами (<c>ExhausterBase</c>), а не интерфейс.
///
/// Сравниваются формы одного и того же экзостера (см.
/// <c>XmlSerDe.Tests.Dispatch</c>): запечатанный наследник; он же со своим
/// <c>override</c>; незапечатанный; и он же через сайт, видевший два типа.
/// Тела у всех форм посимвольно одинаковы, поэтому разница - это цена
/// диспетчеризации и потерянного инлайна, и ничего больше.
///
/// Категорий две, и одна без другой врёт:
/// thin - тело вырождено в одно сложение, вызов виден целиком (верхняя оценка);
/// fat - тело то же, что у настоящего StringBuilderExhauster (живой код).
///
/// Последним в каждой группе идёт control - копия первого случая слово в слово.
/// Разницу между формами читать имеет смысл только если она больше, чем
/// разброс между base и control.
///
/// Формы «невиртуальный метод» здесь больше нет: <c>ExhausterBase</c> объявляет
/// члены abstract. Историческое сравнение с ней - в docs/dispatch-cost.md, и
/// именно оно показало, что запечатанный наследник от неё неотличим.
///
/// Мерялось это сначала самодельной пробой по образцу
/// <see cref="FeatureCostProbe"/>, и та врала: контрольный случай расходился с
/// базой на 13%, то есть шум был больше измеряемого эффекта. Отсюда BDN.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class DispatchMatrixFixture
{
    private const string ThinCategory = "thin";
    private const string FatCategory = "fat";

    private readonly XmlObject2 _obj = new XmlObject2
    {
        IntProperty = 7,
        StringProperty = "abc"
    };

    private readonly ThinSealedExhauster _thinSealed = new ThinSealedExhauster();
    private readonly ThinOpenExhauster _thinOpen = new ThinOpenExhauster();
    private readonly ThinSealedOverrideExhauster _thinSealedOverride = new ThinSealedOverrideExhauster();

    private readonly FatSealedExhauster _fatSealed = new FatSealedExhauster();
    private readonly FatOpenExhauster _fatOpen = new FatOpenExhauster();
    private readonly FatSealedOverrideExhauster _fatSealedOverride = new FatSealedOverrideExhauster();

    #region вырожденное тело: верхняя оценка

    [Benchmark(Baseline = true, Description = "наследник sealed")]
    [BenchmarkCategory(ThinCategory)]
    public long Thin_Sealed()
    {
        ThinSealedHost.Serialize(_thinSealed, _obj, false);
        return _thinSealed.Written;
    }

    [Benchmark(Description = "sealed + свой override")]
    [BenchmarkCategory(ThinCategory)]
    public long Thin_SealedOverride()
    {
        ThinSealedOverrideHost.Serialize(_thinSealedOverride, _obj, false);
        return _thinSealedOverride.Written;
    }

    [Benchmark(Description = "наследник открыт")]
    [BenchmarkCategory(ThinCategory)]
    public long Thin_Open()
    {
        ThinOpenHost.Serialize(_thinOpen, _obj, false);
        return _thinOpen.Written;
    }

    /// <summary>
    /// Через <c>ThinPolyHost</c> ходят оба наследника, поэтому его сайт видел
    /// два типа и угадывать там нечего. Документа тут два за итерацию -
    /// сравнивать эту строку с остальными нужно, поделив пополам.
    /// </summary>
    [Benchmark(Description = "сайт полиморфен (x2 документа)")]
    [BenchmarkCategory(ThinCategory)]
    public long Thin_Polymorphic()
    {
        ThinPolyHost.Serialize(_thinSealedOverride, _obj, false);
        ThinPolyHost.Serialize(_thinOpen, _obj, false);
        return _thinOpen.Written + _thinSealedOverride.Written;
    }

    [Benchmark(Description = "наследник sealed (контроль)")]
    [BenchmarkCategory(ThinCategory)]
    public long Thin_Control()
    {
        ThinSealedHost.Serialize(_thinSealed, _obj, false);
        return _thinSealed.Written;
    }

    #endregion

    #region настоящее тело: то, что видит живой код

    [Benchmark(Baseline = true, Description = "наследник sealed")]
    [BenchmarkCategory(FatCategory)]
    public int Fat_Sealed()
    {
        FatSealedHost.Serialize(_fatSealed, _obj, false);
        var length = _fatSealed.Length;
        _fatSealed.Clear();
        return length;
    }

    [Benchmark(Description = "sealed + свой override")]
    [BenchmarkCategory(FatCategory)]
    public int Fat_SealedOverride()
    {
        FatSealedOverrideHost.Serialize(_fatSealedOverride, _obj, false);
        var length = _fatSealedOverride.Length;
        _fatSealedOverride.Clear();
        return length;
    }

    [Benchmark(Description = "наследник открыт")]
    [BenchmarkCategory(FatCategory)]
    public int Fat_Open()
    {
        FatOpenHost.Serialize(_fatOpen, _obj, false);
        var length = _fatOpen.Length;
        _fatOpen.Clear();
        return length;
    }

    /// <summary>
    /// Тот же полиморфный сайт, что у <see cref="Thin_Polymorphic"/>, но на
    /// настоящем теле. Документа тут два за итерацию.
    /// </summary>
    [Benchmark(Description = "сайт полиморфен (x2 документа)")]
    [BenchmarkCategory(FatCategory)]
    public int Fat_Polymorphic()
    {
        FatPolyHost.Serialize(_fatSealedOverride, _obj, false);
        FatPolyHost.Serialize(_fatOpen, _obj, false);
        var length = _fatOpen.Length + _fatSealedOverride.Length;
        _fatOpen.Clear();
        _fatSealedOverride.Clear();
        return length;
    }

    [Benchmark(Description = "наследник sealed (контроль)")]
    [BenchmarkCategory(FatCategory)]
    public int Fat_Control()
    {
        FatSealedHost.Serialize(_fatSealed, _obj, false);
        var length = _fatSealed.Length;
        _fatSealed.Clear();
        return length;
    }

    #endregion
}
