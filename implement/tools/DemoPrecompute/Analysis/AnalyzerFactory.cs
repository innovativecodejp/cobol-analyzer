using System.Text.Json;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Export;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Engine.Project;

namespace DemoPrecompute.Analysis;

/// <summary>
/// DI コンテナ無しで <see cref="ProjectAnalyzer"/> とエクスポート生成器を組み立てる。
/// MDI 重みは API の <c>appsettings.json</c>（固定重み）から読み、無ければ既定値にフォールバックする。
/// </summary>
internal sealed class AnalyzerFactory
{
    public ProjectAnalyzer ProjectAnalyzer { get; }
    public AnnotationReportGenerator AnnotationReportGenerator { get; }
    public MigrationDesignGenerator MigrationDesignGenerator { get; }
    public MdiWeights Weights { get; }
    public string WeightsSource { get; }

    private AnalyzerFactory(
        ProjectAnalyzer projectAnalyzer,
        AnnotationReportGenerator annotationReportGenerator,
        MigrationDesignGenerator migrationDesignGenerator,
        MdiWeights weights,
        string weightsSource)
    {
        ProjectAnalyzer = projectAnalyzer;
        AnnotationReportGenerator = annotationReportGenerator;
        MigrationDesignGenerator = migrationDesignGenerator;
        Weights = weights;
        WeightsSource = weightsSource;
    }

    public static AnalyzerFactory Create(IReadOnlyList<string> copybookPaths, string? appsettingsPath)
    {
        var (weights, weightsSource) = LoadWeights(appsettingsPath);

        var parser = new CopybookSourceParser(copybookPaths);
        var projectAnalyzer = new ProjectAnalyzer(
            parser,
            new CfgBuilder(),
            new DfgBuilder(),
            new MdiCalculator(weights),
            new CallGraphBuilder(),
            new MigrationRankingBuilder());

        var annotation = new AnnotationReportGenerator(projectAnalyzer);
        var design = new MigrationDesignGenerator(projectAnalyzer);

        return new AnalyzerFactory(projectAnalyzer, annotation, design, weights, weightsSource);
    }

    private static (MdiWeights weights, string source) LoadWeights(string? appsettingsPath)
    {
        if (appsettingsPath is null || !File.Exists(appsettingsPath))
            return (new MdiWeights(), "defaults (appsettings.json 未検出)");

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
            if (!doc.RootElement.TryGetProperty("MdiWeights", out var section))
                return (new MdiWeights(), "defaults (MdiWeights セクション無し)");

            var weights = section.Deserialize<MdiWeights>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new MdiWeights();
            return (weights, appsettingsPath);
        }
        catch (JsonException)
        {
            return (new MdiWeights(), "defaults (appsettings.json パース失敗)");
        }
    }
}
