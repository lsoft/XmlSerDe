using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XmlSerDe;
using XmlSerDe.Tests.Complex;
using XmlSerDe.Tests.Complex.Subject;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Атрибутный путь: сколько стоит голова, по которой ходят несколько раз.
/// Запуск: <c>--attr-cost</c>.
///
/// Зачем отдельный документ. В REGULAR атрибуты стоят на трёх головах из
/// двадцати шести и все они служебные (<c>xmlns:xsi</c> плюс <c>xsi:type</c>),
/// а <c>[XmlAttribute]</c>-членов нет вовсе. Поэтому REGULAR ничего не говорит
/// про POCO, у которого атрибуты несут данные: там на одну голову приходится
/// <c>ReadHead</c>, поиск <c>xsi:type</c> и по отдельному поиску на каждый
/// член, и все они разбирают одни и те же атрибуты заново.
///
/// ATTRS - такой POCO: тридцать нод, у каждой три <c>[XmlAttribute]</c>-члена.
/// REGULAR стоит рядом контрольной строкой: правки атрибутного пути не имеют
/// права его замедлить.
///
/// Вторая строка - тот же документ и тот же тип, но хостом со стражем
/// уникальности имён. Разница между ними и есть цена ещё одного полного
/// обхода головы: страж читает её целиком, поверх всего, что уже прочитано.
/// Число стоит в README («Cost of the attribute path») рядом с объяснением,
/// почему этот обход не слился с остальными.
/// </summary>
internal static class AttributeCostProbe
{
    private const int Warmup = 20_000;
    private const int Iterations = 30_000;
    private const int Rounds = 15;

    public static void Run()
    {
        var pinned = TryPinProcess();

        Console.WriteLine("XmlSerDe attribute-cost probe (in-process, not BenchmarkDotNet)");
        Console.WriteLine(
            $"warmup={Warmup} rounds={Rounds} (best of)"
            + $" runtime={Environment.Version} pinned={pinned}"
            );

        var attrs = AttrXml(30);
        var regular = ComplexFixture.AuxXml;
        var wide = WideXml();

        var cases = new List<Case>
        {
            new Case("ATTRS (30 нод x 3 атрибута-члена)", () =>
            {
                HeadWalkHost.Deserialize(DefaultInjector.Instance, attrs.AsSpan(), out AttrContainer _);
            }),
            new Case("ATTRS + страж уникальности", () =>
            {
                HeadWalkUniqueHost.Deserialize(DefaultInjector.Instance, attrs.AsSpan(), out AttrContainer _);
            }),
            new Case("REGULAR (контроль, xsi:type на месте)", () =>
            {
                GuardLadderDefault.Deserialize(DefaultInjector.Instance, regular.AsSpan(), out InfoContainer _);
            }),
            new Case("WIDE (контроль, длинные головы)", () =>
            {
                GuardLadderDefault.Deserialize(DefaultInjector.Instance, wide.AsSpan(), out InfoContainer _);
            }),
        };

        var best = MeasureAll(cases);

        Console.WriteLine();
        Console.WriteLine("  {0,-40} {1,9}", "документ", "deser ns");
        for (var i = 0; i < cases.Count; i++)
        {
            Console.WriteLine("  {0,-40} {1,9:F0}", cases[i].Name, best[i]);
        }
    }

    private static string AttrXml(int count)
    {
        var sb = new StringBuilder("<AttrContainer><Items>");
        for (var i = 0; i < count; i++)
        {
            sb.Append("<AttrNode id=\"").Append(i)
                .Append("\" tag=\"tag").Append(i)
                .Append("\" kind=\"kind").Append(i)
                .Append("\"><Title>title").Append(i).Append("</Title></AttrNode>");
        }

        sb.Append("</Items></AttrContainer>");
        return sb.ToString();
    }

    private static string WideXml()
    {
        var extras = new StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            extras.Append("attr").Append(i).Append("=\"v").Append(i).Append("\" ");
        }

        return ComplexFixture.AuxXml.Replace("<BaseInfo ", "<BaseInfo " + extras);
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

    private static double[] MeasureAll(List<Case> cases)
    {
        var best = new double[cases.Count];

        for (var i = 0; i < cases.Count; i++)
        {
            best[i] = double.MaxValue;
            for (var w = 0; w < Warmup; w++)
            {
                cases[i].Action();
            }
        }

        for (var round = 0; round < Rounds; round++)
        {
            for (var k = 0; k < cases.Count; k++)
            {
                var i = (round % 2 == 0) ? k : cases.Count - 1 - k;
                var action = cases[i].Action;

                var sw = Stopwatch.StartNew();
                for (var n = 0; n < Iterations; n++)
                {
                    action();
                }
                sw.Stop();

                var ns = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / Iterations;
                if (ns < best[i])
                {
                    best[i] = ns;
                }
            }
        }

        return best;
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
