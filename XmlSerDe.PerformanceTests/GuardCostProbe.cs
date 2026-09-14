using System;
using System.Collections.Generic;
using System.Diagnostics;
using XmlSerDe;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep;
using XmlSerDe.Tests.Deep.Subject;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Цена включённого стража, docs/opt-in-xml-guards.md §12. Инструмент тот же,
/// что у <see cref="FeatureCostProbe"/>, и по той же причине: вопрос здесь -
/// не «сколько стоит десериализация», а «сколько стоит <b>дельта</b> от одного
/// флага», и она лежит в единицах процентов. Запуск: <c>--guard-cost</c>.
///
/// Меряются две лестницы (REGULAR и DEEP) плюс две контрольные строки:
///
/// 1. always-on разбор атрибута (§5.1) платят <b>все</b> хосты, включая
///    default. Отдельной строки у него быть не может - выключателя нет, -
///    поэтому здесь стоит абсолютное время default, которое сравнивается
///    с прошлым прогоном.
/// 2. сверка имени закрывающего тега у <b>скалярного</b> члена включена всем
///    и всегда (§5.7). Это открытый вопрос: если её цена выше шума, она
///    переезжает под <see cref="XmlSerDe.XmlGuard.MatchingEndTags"/>,
///    и default станет быстрее сегодняшнего.
/// </summary>
internal static class GuardCostProbe
{
    private const int Warmup = 20_000;
    private const int LadderIterations = 30_000;
    private const int DeepIterations = 10_000;
    private const int LadderRounds = 15;

    public static void Run()
    {
        var pinned = TryPinProcess();

        Console.WriteLine("XmlSerDe guard-cost probe (in-process, not BenchmarkDotNet)");
        Console.WriteLine(
            $"warmup={Warmup} rounds={LadderRounds} (best of)"
            + $" runtime={Environment.Version} pinned={pinned}"
            );

        RunRegularLadder();
        RunWideLadder();
        RunDeepLadder();
        RunScalarEndTagProbe();
    }

    /// <summary>
    /// Тот же REGULAR, но каждая голова несёт ещё десяток атрибутов, которых
    /// хост не знает. Разбору они безразличны - он ищет свои по имени, -
    /// а <see cref="XmlSerDe.XmlGuard.UniqueAttributes"/> обязан
    /// пройти по всем.
    ///
    /// Строка нужна потому, что на POCO-голове с двумя атрибутами обе
    /// реализации проверки уникальности стоят одинаково, и выбрать между ними
    /// по REGULAR нельзя: разница там меньше дрейфа. Здесь она видна -
    /// попарная проверка квадратична по числу атрибутов, проверка через буфер
    /// на стеке линейна.
    /// </summary>
    private static void RunWideLadder()
    {
        var xml = WideXml();

        var cases = new List<Case>
        {
            new Case("default (стражей нет)", () =>
            {
                GuardLadderDefault.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("UniqueAttributes", () =>
            {
                GuardLadderOnlyUniqueAttributes.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("SystemXmlCompatible", () =>
            {
                GuardLadderFull.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
        };

        Print("широкие головы (REGULAR + 10 чужих атрибутов на голову)", cases, LadderIterations, marginal: false);
    }

    private static string WideXml()
    {
        var extras = new System.Text.StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            extras.Append("attr").Append(i).Append("=\"v").Append(i).Append("\" ");
        }

        return ComplexFixture.AuxXml.Replace("<BaseInfo ", "<BaseInfo " + extras);
    }

    /// <summary>
    /// REGULAR (<c>ComplexFixture.AuxXml</c>): 26 элементов, три
    /// <c>xsi:type</c>, экранированные строки. Здесь платят все четыре стража.
    /// </summary>
    private static void RunRegularLadder()
    {
        var xml = ComplexFixture.AuxXml;

        var single = new List<Case>
        {
            new Case("default (стражей нет)", () =>
            {
                GuardLadderDefault.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("MatchingEndTags", () =>
            {
                GuardLadderOnlyMatchingEndTags.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("SingleRoot", () =>
            {
                GuardLadderOnlySingleRoot.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("UniqueAttributes", () =>
            {
                GuardLadderOnlyUniqueAttributes.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("IllegalChars", () =>
            {
                GuardLadderOnlyIllegalChars.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
        };

        var cumulative = new List<Case>
        {
            single[0],
            single[1],
            new Case("+ SingleRoot", () =>
            {
                GuardLadderPlusSingleRoot.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("+ UniqueAttributes", () =>
            {
                GuardLadderPlusUniqueAttributes.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("+ IllegalChars = SystemXmlCompatible", () =>
            {
                GuardLadderFull.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
        };

        Print("по одному стражу поверх default (REGULAR)", single, LadderIterations, marginal: false);
        Print("накопительно (REGULAR)", cumulative, LadderIterations, marginal: true);
    }

    /// <summary>
    /// DEEP: 100 уровней, ни одного атрибута, одна строка. Здесь платит только
    /// <c>MatchingEndTags</c> - и платит по сравнению имени на каждый уровень.
    /// </summary>
    private static void RunDeepLadder()
    {
        var xml = DeepFixture.DeepXml;

        var cases = new List<Case>
        {
            new Case("default (стражей нет)", () =>
            {
                GuardDeepLadderDefault.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out DeepNode _);
            }),
            new Case("MatchingEndTags", () =>
            {
                GuardDeepLadderOnlyMatchingEndTags.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out DeepNode _);
            }),
            new Case("SystemXmlCompatible", () =>
            {
                GuardDeepLadderFull.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out DeepNode _);
            }),
        };

        Print("лестница (DEEP, 100 уровней)", cases, DeepIterations, marginal: false);
    }

    /// <summary>
    /// Контрольная строка §5.7: сколько default платит за сверку имени
    /// закрывающего тега скалярного члена. Выключателя у неё нет, поэтому
    /// цена оценивается по документу, где скалярных членов много, против
    /// документа той же длины, где их нет: разница между строками - это верхняя
    /// граница того, что можно было бы вернуть, переведя сверку под флаг.
    /// </summary>
    private static void RunScalarEndTagProbe()
    {
        var xml = ComplexFixture.AuxXml;
        var deep = DeepFixture.DeepXml;

        Console.WriteLine();
        Console.WriteLine("контрольные строки (§5.1 always-on, §5.7 скалярная сверка):");
        Console.WriteLine("  абсолютное время default - его и сравнивают с прошлым прогоном;");
        Console.WriteLine("  оно включает always-on разбор атрибута, у которого выключателя нет.");

        var cases = new List<Case>
        {
            new Case("REGULAR default", () =>
            {
                GuardLadderDefault.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _);
            }),
            new Case("DEEP default", () =>
            {
                GuardDeepLadderDefault.Deserialize(DefaultInjector.Instance, deep.AsSpan(), out DeepNode _);
            }),
        };

        var best = MeasureAll(cases, LadderIterations);

        for (var i = 0; i < cases.Count; i++)
        {
            Console.WriteLine($"  {cases[i].Name,-40} {best[i],9:F0} ns/op");
        }
    }

    private static void Print(string title, List<Case> cases, int iterations, bool marginal)
    {
        var best = MeasureAll(cases, iterations, out var alloc);

        Console.WriteLine();
        Console.WriteLine(title + ":");
        Console.WriteLine(
            "  {0,-40} {1,9} {2,7} {3,7} {4,8}",
            "шаг",
            "deser ns",
            "x base",
            marginal ? "x пред" : "",
            "B/op"
            );

        for (var i = 0; i < cases.Count; i++)
        {
            var prev = i == 0 ? best[0] : best[i - 1];

            Console.WriteLine(
                "  {0,-40} {1,9:F0} {2,7:F3} {3,7} {4,8:F0}",
                cases[i].Name,
                best[i],
                best[i] / best[0],
                marginal ? (best[i] / prev).ToString("F3") : "",
                alloc[i]
                );
        }
    }

    private readonly struct Case
    {
        public readonly string Name;
        public readonly Action Action;

        public Case(string name, Action action)
        {
            Name = name;
            Action = action;
        }
    }

    /// <summary>
    /// Тот же способ измерения, что у <see cref="FeatureCostProbe"/>: прогрев,
    /// раунды снаружи по списку, направление чередуется, в отчёт идёт минимум.
    /// Мерить случаи по очереди нельзя - первый впитывает прогрев общего кода.
    /// </summary>
    private static double[] MeasureAll(List<Case> cases, int iterations)
    {
        return MeasureAll(cases, iterations, out _);
    }

    /// <summary>
    /// Колонка B/op здесь не про производительность, а про запрет: стражи не
    /// имеют права выделять на happy path - ни списка имён атрибутов, ни
    /// читателя. Выросшее относительно default число означает, что где-то
    /// завелась куча (docs/opt-in-xml-guards.md §13.3).
    /// </summary>
    private static double[] MeasureAll(List<Case> cases, int iterations, out double[] alloc)
    {
        var best = new double[cases.Count];
        alloc = new double[cases.Count];

        for (var i = 0; i < cases.Count; i++)
        {
            best[i] = double.MaxValue;

            for (var w = 0; w < Warmup; w++)
            {
                cases[i].Action();
            }
        }

        for (var round = 0; round < LadderRounds; round++)
        {
            for (var k = 0; k < cases.Count; k++)
            {
                var i = (round % 2 == 0) ? k : cases.Count - 1 - k;
                var action = cases[i].Action;

                var allocBefore = AllocatedBytes();
                var sw = Stopwatch.StartNew();
                for (var n = 0; n < iterations; n++)
                {
                    action();
                }
                sw.Stop();
                var allocated = AllocatedBytes() - allocBefore;

                var ns = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
                if (ns < best[i])
                {
                    best[i] = ns;
                }

                alloc[i] = allocated / (double)iterations;
            }
        }

        return best;
    }

    //на net472 нет GetAllocatedBytesForCurrentThread; там колонка B/op
    //считается по GC.GetTotalMemory и годится только на порядок величины
    private static long AllocatedBytes()
    {
#if NET8_0_OR_GREATER
        return GC.GetAllocatedBytesForCurrentThread();
#else
        return GC.GetTotalMemory(false);
#endif
    }

    private static bool TryPinProcess()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;
            var cpu = Math.Min(2, Environment.ProcessorCount - 1);
            process.ProcessorAffinity = new IntPtr(1L << cpu);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
