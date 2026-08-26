using System;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace XmlSerDe.PerformanceTests;

/// <summary>
/// Labels for the Exhauster column. They are not part of the method name.
/// </summary>
internal static class Sinks
{
    public const string StringBuilder = "stringbuilder";
    public const string PooledChar = "pooledchar";
    public const string MemoryStream = "memorystream";
    public const string EmptyStream = "emptystream";
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SinkAttribute : Attribute
{
    public string Value { get; }

    public SinkAttribute(string value)
    {
        Value = value;
    }
}

/// <summary>
/// Marks a two-phase serialize: estimate length, then write into a pre-sized exhauster.
/// Absence means the method does not pre-estimate.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PreestimateAttribute : Attribute
{
}

/// <summary>
/// Job characteristic columns (Method, Job, Runtime, …) all have
/// <see cref="IColumn.PriorityInCategory"/> 0, so extra Job-category columns
/// appended later land after Runtime. Hide the stock Runtime column by Id and
/// re-add it with a priority after the meta columns.
/// </summary>
internal static class ReportLayout
{
    internal const int CategoriesPriority = 1;
    internal const int ExhausterPriority = 2;
    internal const int PreestimatePriority = 3;
    internal const int RuntimePriority = 10;

    internal const string MemoryStreamGroupSuffix = "-MEMORYSTREAM";

    private static readonly IColumn StockRuntime = JobCharacteristicColumn.AllColumns
        .Single(column => column.ColumnName == Column.Runtime);

    internal static ManualConfig Create(params IColumn[] metaColumns)
    {
        var config = ManualConfig.CreateEmpty()
            .HideColumns(Column.Job)
            .HideColumns(StockRuntime)
            .HideColumns(CategoriesColumn.Default);

        foreach (var column in metaColumns)
        {
            config.AddColumn(column);
        }

        return config.AddColumn(new ReorderedRuntimeColumn());
    }

    private sealed class ReorderedRuntimeColumn : IColumn
    {
        public string Id => nameof(ReorderedRuntimeColumn);
        public string ColumnName => StockRuntime.ColumnName;
        public bool AlwaysShow => StockRuntime.AlwaysShow;
        public ColumnCategory Category => ColumnCategory.Job;
        public int PriorityInCategory => RuntimePriority;
        public bool IsNumeric => StockRuntime.IsNumeric;
        public UnitType UnitType => StockRuntime.UnitType;
        public string Legend => StockRuntime.Legend;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
            => StockRuntime.GetValue(summary, benchmarkCase);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
            => StockRuntime.GetValue(summary, benchmarkCase, style);

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase)
            => StockRuntime.IsDefault(summary, benchmarkCase);

        public bool IsAvailable(Summary summary) => StockRuntime.IsAvailable(summary);
    }
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class SerializeReportColumnsAttribute : Attribute, IConfigSource
{
    public IConfig Config { get; } = ReportLayout.Create(
        new ShapeCategoriesColumn(),
        new ExhausterColumn(),
        new PreestimateColumn());
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class DeserializeReportColumnsAttribute : Attribute, IConfigSource
{
    public IConfig Config { get; } = ReportLayout.Create(
        new ShapeCategoriesColumn());
}

internal sealed class ShapeCategoriesColumn : IColumn
{
    public string Id => nameof(ShapeCategoriesColumn);
    public string ColumnName => "Categories";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Job;
    public int PriorityInCategory => ReportLayout.CategoriesPriority;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Document shape: REGULAR, HUGE, DEEP";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var category = benchmarkCase.Descriptor.Categories.FirstOrDefault();
        if (string.IsNullOrEmpty(category))
        {
            return "-";
        }

        if (category.EndsWith(ReportLayout.MemoryStreamGroupSuffix, StringComparison.Ordinal))
        {
            return category.Substring(0, category.Length - ReportLayout.MemoryStreamGroupSuffix.Length);
        }

        return category;
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public bool IsAvailable(Summary summary) => true;
}

internal sealed class ExhausterColumn : IColumn
{
    public string Id => nameof(ExhausterColumn);
    public string ColumnName => "Exhauster";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Job;
    public int PriorityInCategory => ReportLayout.ExhausterPriority;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "stringbuilder = StringBuilderExhauster / StringWriter; pooledchar = PooledCharExhauster; memorystream = buffered UTF-8 MemoryStream; emptystream = Utf8BinaryExhausterEmpty";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var exhauster = benchmarkCase.Descriptor.WorkloadMethod.GetCustomAttribute<SinkAttribute>();
        return exhauster is null ? "-" : exhauster.Value;
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public bool IsAvailable(Summary summary) => true;
}

internal sealed class PreestimateColumn : IColumn
{
    public string Id => nameof(PreestimateColumn);
    public string ColumnName => "Preestimate";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Job;
    public int PriorityInCategory => ReportLayout.PreestimatePriority;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "true = LengthEstimatorExhauster walk, then a pre-sized PooledCharExhauster or MemoryStream at 1.1× the estimate";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var method = benchmarkCase.Descriptor.WorkloadMethod;
        return method.GetCustomAttribute<PreestimateAttribute>() is null ? "false" : "true";
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public bool IsAvailable(Summary summary) => true;
}
