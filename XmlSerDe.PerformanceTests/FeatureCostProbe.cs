using System;
using System.Collections.Generic;
using System.Diagnostics;
using XmlSerDe;
using XmlSerDe.Tests;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// In-process timing for the README opt-in cost table. Not a BenchmarkDotNet
/// job: run with <c>--feature-cost</c>. Official REGULAR/DEEP numbers still
/// come from <see cref="DeserializeMatrixFixture"/> / <see cref="SerializeMatrixFixture"/>.
///
/// Все замеры сначала собираются в список и только потом исполняются, по
/// <see cref="Rounds"/> раундов на каждый, и в отчёт идёт минимум. Иначе первый
/// в списке впитывает прогрев всего общего кода: измерение «default против
/// SystemXmlCompatible» в один проход по порядку давало разницу в 2 раза
/// исключительно из-за очерёдности.
/// </summary>
internal static class FeatureCostProbe
{
    private const int Warmup = 20_000;
    private const int DocumentIterations = 100_000;
    private const int MicroIterations = 500_000;
    private const int Rounds = 5;
    private const int LadderIterations = 30_000;
    private const int LadderRounds = 15;

    private const string Poco =
        "<XmlObject2><IntProperty>7</IntProperty><StringProperty>abc</StringProperty></XmlObject2>";

    public static void Run()
    {
        var pinned = TryPinProcess();

        Console.WriteLine("XmlSerDe feature-cost probe (in-process, not BenchmarkDotNet)");
        Console.WriteLine(
            $"warmup={Warmup} rounds={Rounds}/{LadderRounds} (best of)"
            + $" runtime={Environment.Version} pinned={pinned}"
            );

        var sb = new StringBuilderExhauster();
        var obj = new XmlObject2 { IntProperty = 7, StringProperty = "abc" };

        RunLadder(sb);

        RunGroup(
            "документы целиком",
            DocumentIterations,
            new List<Case>
            {
                new Case("REGULAR AuxXml default deserialize", () =>
                {
                    ComplexFixture.Deserialize_XmlSerDe(ComplexFixture.AuxXml.AsSpan());
                }),
                new Case("REGULAR AuxXml SystemXmlCompatible deserialize", () =>
                {
                    ComplexFixture.Deserialize_XmlSerDe_Compatible(ComplexFixture.AuxXml.AsSpan());
                }),
                new Case("REGULAR AuxXmlLegacy (CDATA + p3) SystemXmlCompatible", () =>
                {
                    ComplexFixture.Deserialize_XmlSerDe_Compatible(ComplexFixture.AuxXmlLegacy.AsSpan());
                }),
                new Case("DEEP default deserialize", () =>
                {
                    DeepFixture.Deserialize_XmlSerDe(DeepFixture.DeepXml.AsSpan());
                }),
                new Case("REGULAR default serialize (StringBuilderExhauster)", () =>
                {
                    sb.Clear();
                    XmlSerDe.Tests.Complex.XmlSerializerDeserializer.Serialize(
                        sb,
                        ComplexFixture.DefaultObject,
                        false
                        );
                }),
                new Case("REGULAR SystemXmlCompatible serialize", () =>
                {
                    sb.Clear();
                    XmlSerializerDeserializerCompatible.Serialize(
                        sb,
                        ComplexFixture.DefaultObject,
                        false
                        );
                }),
            });

        RunGroup(
            "XmlObject2 POCO document, per-flag deserialize (same XML, different hosts)",
            MicroIterations,
            new List<Case>
            {
                new Case("default", () =>
                {
                    XmlSerializerDeserializer2.Deserialize(DefaultInjector.Instance, Poco.AsSpan(), out XmlObject2 _);
                }),
                new Case("Markup", () =>
                {
                    XmlSerializerDeserializerMarkup.Deserialize(DefaultInjector.Instance, Poco.AsSpan(), out XmlObject2 _);
                }),
                new Case("CData", () =>
                {
                    XmlSerializerDeserializerCData.Deserialize(DefaultInjector.Instance, Poco.AsSpan(), out XmlObject2 _);
                }),
                new Case("FlexibleXsiPrefix", () =>
                {
                    XmlSerializerDeserializerFlexibleXsi.Deserialize(DefaultInjector.Instance, Poco.AsSpan(), out XmlObject2 _);
                }),
                new Case("SystemXmlCompatible", () =>
                {
                    XmlSerializerDeserializerFull.Deserialize(DefaultInjector.Instance, Poco.AsSpan(), out XmlObject2 _);
                }),
            });

        RunGroup(
            "XmlObject2 POCO document, serialize",
            MicroIterations,
            new List<Case>
            {
                new Case("default serialize", () =>
                {
                    sb.Clear();
                    XmlSerializerDeserializer2.Serialize(sb, obj, false);
                }),
                new Case("CharGuard serialize", () =>
                {
                    sb.Clear();
                    XmlSerializerDeserializerCharGuard.Serialize(sb, obj, false);
                }),
            });
    }

    /// <summary>
    /// Лестница фич на одном документе (REGULAR / <c>ComplexFixture.AuxXml</c>)
    /// и одном графе типов. Документ выбран самым показательным из имеющихся:
    /// 26 элементов даёт цену <c>ReadHead</c> на тег, три <c>xsi:type</c> -
    /// цену разбора атрибутов и поиска префикса, экранированные строки -
    /// цену декодера и (на сериализации) guard'а.
    ///
    /// Вход у всех хостов один и тот же и ни одной opt-in-конструкции не
    /// содержит: измеряется цена самой возможности их понимать, а не цена
    /// другого документа.
    /// </summary>
    private static void RunLadder(StringBuilderExhauster sb)
    {
        var xml = ComplexFixture.AuxXml;
        var obj = ComplexFixture.DefaultObject;

        var steps = new List<LadderStep>
        {
            new LadderStep(
                "default (флагов нет)",
                () => { LadderDefault.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderDefault.Serialize(sb, obj, false); }),
            new LadderStep(
                "Markup",
                () => { LadderOnlyMarkup.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyMarkup.Serialize(sb, obj, false); }),
            new LadderStep(
                "CData",
                () => { LadderOnlyCData.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyCData.Serialize(sb, obj, false); }),
            new LadderStep(
                "FlexibleXsiPrefix",
                () => { LadderOnlyFlexibleXsi.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyFlexibleXsi.Serialize(sb, obj, false); }),
            new LadderStep(
                "CharGuard",
                () => { LadderOnlyCharGuard.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyCharGuard.Serialize(sb, obj, false); }),
        };

        var cumulative = new List<LadderStep>
        {
            steps[0],
            new LadderStep(
                "+ Markup",
                () => { LadderOnlyMarkup.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyMarkup.Serialize(sb, obj, false); }),
            new LadderStep(
                "+ CData",
                () => { LadderOnlyCData.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderOnlyCData.Serialize(sb, obj, false); }),
            new LadderStep(
                "+ FlexibleXsiPrefix",
                () => { LadderPlusFlexibleXsi.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderPlusFlexibleXsi.Serialize(sb, obj, false); }),
            new LadderStep(
                "+ CharGuard = SystemXmlCompatible",
                () => { LadderPlusCharGuard.Deserialize(DefaultInjector.Instance, xml.AsSpan(), out InfoContainer _); },
                () => { sb.Clear(); LadderPlusCharGuard.Serialize(sb, obj, false); }),
        };

        PrintLadder("по одной фиче поверх default (REGULAR)", steps, marginal: false);
        PrintLadder("накопительно (REGULAR)", cumulative, marginal: true);
    }

    private readonly struct LadderStep
    {
        public readonly string Name;
        public readonly Action Deserialize;
        public readonly Action Serialize;

        public LadderStep(string name, Action deserialize, Action serialize)
        {
            Name = name;
            Deserialize = deserialize;
            Serialize = serialize;
        }
    }

    private static void PrintLadder(string title, List<LadderStep> steps, bool marginal)
    {
        //десериализация и сериализация меряются одним round-robin: если гонять
        //их отдельными проходами, между проходами успевает уехать частота, и
        //база одной таблицы оказывается несравнима с базой другой
        var cases = new List<Case>();
        foreach (var step in steps)
        {
            cases.Add(new Case(step.Name, step.Deserialize));
        }
        foreach (var step in steps)
        {
            cases.Add(new Case(step.Name, step.Serialize));
        }

        var measured = MeasureAll(cases, LadderIterations, LadderRounds, out _);

        var d = new double[steps.Count];
        var s = new double[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            d[i] = measured[i];
            s[i] = measured[steps.Count + i];
        }

        Console.WriteLine();
        Console.WriteLine(title + ":");
        Console.WriteLine(
            "  {0,-36} {1,9} {2,7} {3,7} | {4,9} {5,7} {6,7}",
            "шаг",
            "deser ns",
            "x base",
            marginal ? "x пред" : "",
            "ser ns",
            "x base",
            marginal ? "x пред" : ""
            );

        for (var i = 0; i < steps.Count; i++)
        {
            var dPrev = i == 0 ? d[0] : d[i - 1];
            var sPrev = i == 0 ? s[0] : s[i - 1];

            Console.WriteLine(
                "  {0,-36} {1,9:F0} {2,7:F3} {3,7} | {4,9:F0} {5,7:F3} {6,7}",
                steps[i].Name,
                d[i],
                d[i] / d[0],
                marginal ? (d[i] / dPrev).ToString("F3") : "",
                s[i],
                s[i] / s[0],
                marginal ? (s[i] / sPrev).ToString("F3") : ""
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
    /// Прибивает процесс к одному ядру и поднимает приоритет. Разница между
    /// соседними шагами лестницы - единицы процентов, а планировщик и переезд
    /// между ядрами дают разброс того же порядка; без этого таблица шумит
    /// сильнее, чем измеряемый эффект. Если не получилось (нет прав, не та ОС) -
    /// просто меряем как есть, о чём и сообщаем в шапке.
    /// </summary>
    private static bool TryPinProcess()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;
            //не нулевой логический процессор (на него садятся прерывания) и не
            //последний: на гибридных Intel хвост нумерации - это E-ядра, и
            //абсолютные числа уезжали бы в полтора раза
            var cpu = Math.Min(2, Environment.ProcessorCount - 1);
            process.ProcessorAffinity = new IntPtr(1L << cpu);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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

    private static void RunGroup(string title, int iterations, List<Case> cases)
    {
        var best = MeasureAll(cases, iterations, out var alloc);

        Console.WriteLine();
        Console.WriteLine(title + ":");

        var baseline = best.Length > 0 ? best[0] : 1.0;
        for (var i = 0; i < cases.Count; i++)
        {
            Console.WriteLine(
                $"  {cases[i].Name,-52} {best[i],8:F0} ns/op {alloc[i],7:F0} B/op  x{best[i] / baseline:F2}"
                );
        }
    }

    private static double[] MeasureAll(List<Case> cases, int iterations, out double[] alloc)
    {
        return MeasureAll(cases, iterations, Rounds, out alloc);
    }

    private static double[] MeasureAll(List<Case> cases, int iterations, int rounds, out double[] alloc)
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

        //раунды снаружи по списку, а не внутри одного замера: так ни один
        //случай не оказывается систематически первым
        for (var round = 0; round < rounds; round++)
        {
            //направление чередуется, чтобы монотонный дрейф частоты не оседал
            //систематически на первых или на последних случаях списка
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
}
