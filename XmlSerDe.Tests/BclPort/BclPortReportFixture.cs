#nullable disable

using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using XmlSerDe.Tests.Interop;

namespace XmlSerDe.Tests.BclPort
{
    /// <summary>
    /// То же, что <see cref="InteropReportFixture"/>, но для чужого корпуса:
    /// прогоняет перенесённые из <c>System.Xml.Serialization</c> формы и
    /// выкладывает таблицу рядом с тестовой сборкой (<c>bcl-port-report.md</c>).
    ///
    /// Утверждений здесь нет: они живут в <see cref="BclPortFixture"/> по одному
    /// на форму. Отчёт нужен, чтобы после правки генератора было видно не только
    /// "сколько тестов красных", но и чем именно расходятся стороны.
    /// </summary>
    public class BclPortReportFixture
    {
        private readonly ITestOutputHelper _output;

        public BclPortReportFixture(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BclPort_Report()
        {
            var sb = new StringBuilder();
            var compatible = 0;
            var total = 0;

            sb.AppendLine("| Форма | XmlSerDe читает System.Xml | System.Xml читает XmlSerDe | Формат совпадает |");
            sb.AppendLine("|---|---|---|---|");

            var details = new StringBuilder();

            foreach (var portCase in BclPortCorpus.All)
            {
                total++;

                InteropResult result;
                try
                {
                    result = portCase.Run();
                }
                catch (Exception excp)
                {
                    sb.AppendLine($"| {portCase.Name} | ОШИБКА | ОШИБКА | ОШИБКА |");
                    details.AppendLine($"### {portCase.Name}");
                    details.AppendLine(excp.GetType().Name + ": " + excp.Message);
                    details.AppendLine();
                    continue;
                }

                if (result.FullyCompatible)
                {
                    compatible++;
                }

                sb.AppendLine(
                    $"| {portCase.Name} "
                    + $"| {Mark(result.CanReadSystemXml)} "
                    + $"| {Mark(result.SystemXmlCanReadOurs)} "
                    + $"| {Mark(result.SameShape)} |"
                    );

                details.AppendLine($"### {portCase.Name}");
                details.AppendLine(result.Describe());
                details.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine($"Полностью совместимо: {compatible} из {total}.");
            sb.AppendLine();
            sb.AppendLine(details.ToString());

            var report = sb.ToString();
            _output.WriteLine(report);

            var path = Path.Combine(AppContext.BaseDirectory, "bcl-port-report.md");
            File.WriteAllText(path, report);
        }

        private static string Mark(bool value) => value ? "да" : "НЕТ";
    }
}
