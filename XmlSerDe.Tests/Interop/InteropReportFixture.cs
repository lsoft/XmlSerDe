#nullable disable

using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Прогоняет весь корпус и выкладывает таблицу совместимости рядом с тестовой сборкой.
    /// Не утверждает ничего: утверждения живут в <see cref="InteropFixture"/> по одному
    /// на форму. Смысл отчёта в том, чтобы степень drop-in можно было измерить, а не
    /// обсуждать - и чтобы после каждой правки генератора было видно, что именно сдвинулось.
    /// </summary>
    public class InteropReportFixture
    {
        private readonly ITestOutputHelper _output;

        public InteropReportFixture(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Interop_Report()
        {
            var sb = new StringBuilder();
            var compatible = 0;
            var total = 0;

            sb.AppendLine("| Форма | XmlSerDe читает System.Xml | System.Xml читает XmlSerDe | Формат совпадает |");
            sb.AppendLine("|---|---|---|---|");

            var details = new StringBuilder();

            foreach (var interopCase in InteropCorpus.All)
            {
                total++;

                InteropResult result;
                try
                {
                    result = interopCase.Run();
                }
                catch (Exception excp)
                {
                    sb.AppendLine($"| {interopCase.Name} | ОШИБКА | ОШИБКА | ОШИБКА |");
                    details.AppendLine($"### {interopCase.Name}");
                    details.AppendLine(excp.GetType().Name + ": " + excp.Message);
                    details.AppendLine();
                    continue;
                }

                if (result.FullyCompatible)
                {
                    compatible++;
                }

                sb.AppendLine(
                    $"| {interopCase.Name} "
                    + $"| {Mark(result.CanReadSystemXml)} "
                    + $"| {Mark(result.SystemXmlCanReadOurs)} "
                    + $"| {Mark(result.SameShape)} |"
                    );

                details.AppendLine($"### {interopCase.Name}");
                details.AppendLine(result.Describe());
                details.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine($"Полностью совместимо: {compatible} из {total}.");
            sb.AppendLine();
            sb.AppendLine(details.ToString());

            var report = sb.ToString();
            _output.WriteLine(report);

            var path = Path.Combine(AppContext.BaseDirectory, "interop-report.md");
            File.WriteAllText(path, report);
        }

        private static string Mark(bool value) => value ? "да" : "НЕТ";
    }
}
