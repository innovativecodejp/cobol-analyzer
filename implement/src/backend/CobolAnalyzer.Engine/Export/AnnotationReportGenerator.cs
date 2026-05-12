using System.Text;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Comment;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Project;

namespace CobolAnalyzer.Engine.Export;

public class AnnotationReportGenerator
{
    private readonly IProjectAnalyzer _projectAnalyzer;

    public AnnotationReportGenerator(IProjectAnalyzer projectAnalyzer)
    {
        _projectAnalyzer = projectAnalyzer;
    }

    public string Generate(string fileName, string source)
    {
        var project = _projectAnalyzer.Analyze(new[]
        {
            new CobolSource(fileName, source)
        });

        var result = project.Programs.FirstOrDefault() ?? new AnalyzeResult();
        var programName = result.Metrics?.ProgramName
            ?? result.Ast?.Name
            ?? Path.GetFileNameWithoutExtension(fileName)
            ?? "UNKNOWN";

        var tags = ExtractTags(source);
        var sb = new StringBuilder();

        sb.AppendLine($"# COBOL 移行分析レポート：{programName}");
        sb.AppendLine();
        sb.AppendLine($"生成日: {DateTime.Today:yyyy-MM-dd}");
        sb.AppendLine($"ファイル名: {fileName}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        if (result.Errors.Count > 0 || result.Metrics is null)
        {
            AppendParseErrors(sb, result);
            AppendTags(sb, tags);
            return sb.ToString();
        }

        AppendMdiSummary(sb, result);
        AppendMetricsBreakdown(sb, result);
        AppendRiskPatterns(sb, result);
        AppendTags(sb, tags);
        AppendStrategy(sb, MigrationRankingBuilder.DetermineStrategy(result.Metrics.Mdi.Score, 0, 0));

        return sb.ToString();
    }

    private static void AppendParseErrors(StringBuilder sb, AnalyzeResult result)
    {
        sb.AppendLine("## 解析エラー");
        sb.AppendLine();

        if (result.Errors.Count == 0)
        {
            sb.AppendLine("解析結果を生成できませんでした。");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 行 | 列 | メッセージ |");
        sb.AppendLine("|---|---|------------|");
        foreach (var error in result.Errors)
            sb.AppendLine($"| {error.Line} | {error.Column} | {MarkdownEscaper.TableCell(error.Message)} |");
        sb.AppendLine();
    }

    private static void AppendMdiSummary(StringBuilder sb, AnalyzeResult result)
    {
        var mdi = result.Metrics!.Mdi;

        sb.AppendLine("## MDI サマリー");
        sb.AppendLine();
        sb.AppendLine("| 指標 | スコア | リスクランク |");
        sb.AppendLine("|------|--------|------------|");
        sb.AppendLine($"| 総合MDI | {mdi.Score:F1} | {mdi.Risk} |");
        sb.AppendLine();
    }

    private static void AppendMetricsBreakdown(StringBuilder sb, AnalyzeResult result)
    {
        var metrics = result.Metrics!;
        var contributions = metrics.Mdi.WeightedContributions;

        sb.AppendLine("## 指標内訳");
        sb.AppendLine();
        sb.AppendLine("| 指標ID | 指標名 | 実測値 | 寄与スコア |");
        sb.AppendLine("|--------|--------|--------|-----------|");
        AppendMetricRow(sb, "CC", "サイクロマティック複雑度", metrics.CyclomaticComplexity, contributions);
        AppendMetricRow(sb, "GD", "GO TO 密度", metrics.GoToDensity, contributions, "F3");
        AppendMetricRow(sb, "AD", "ALTER 文数", metrics.AlterCount, contributions);
        AppendMetricRow(sb, "ND", "ネスト深度", metrics.MaxNestingDepth, contributions);
        AppendMetricRow(sb, "RD", "REDEFINES 密度", metrics.RedefinesDensity, contributions, "F3");
        AppendMetricRow(sb, "CS", "スコープ横断依存数", metrics.CrossScopeDependencies, contributions);
        sb.AppendLine();
    }

    private static void AppendMetricRow(
        StringBuilder sb,
        string id,
        string name,
        double value,
        IReadOnlyDictionary<string, double> contributions,
        string format = "F0")
    {
        var contribution = contributions.GetValueOrDefault(id);
        sb.AppendLine($"| {id} | {name} | {value.ToString(format)} | {contribution:F2} |");
    }

    private static void AppendRiskPatterns(StringBuilder sb, AnalyzeResult result)
    {
        var metrics = result.Metrics!;
        var redefinesCount = result.Dfg?.Edges.Count(e => e.Kind == DfgEdgeKind.Redefines) ?? 0;
        var hasAny = false;

        sb.AppendLine("## 高リスクパターン");
        sb.AppendLine();

        if (metrics.GoToDensity > 0)
        {
            sb.AppendLine($"- GO TO 密度が {metrics.GoToDensity:F3} です（非構造化制御フロー）");
            hasAny = true;
        }

        if (metrics.AlterCount > 0)
        {
            sb.AppendLine($"- ALTER 文が {metrics.AlterCount} 件存在します（動的制御フロー変更・高リスク）");
            hasAny = true;
        }

        if (redefinesCount > 0)
        {
            sb.AppendLine($"- REDEFINES が {redefinesCount} 件存在します");
            hasAny = true;
        }

        if (metrics.MaxNestingDepth >= 4)
        {
            sb.AppendLine($"- ネスト深度が {metrics.MaxNestingDepth} 階層あります");
            hasAny = true;
        }

        if (!hasAny)
            sb.AppendLine("なし");

        sb.AppendLine();
    }

    private static void AppendTags(StringBuilder sb, IReadOnlyList<TaggedComment> tags)
    {
        sb.AppendLine("## タグ付きコメント一覧");
        sb.AppendLine();

        if (tags.Count == 0)
        {
            sb.AppendLine("なし");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 行番号 | タグ | 値 | メッセージ |");
        sb.AppendLine("|--------|-----|---|-----------|");
        foreach (var tag in tags)
        {
            sb.AppendLine(
                $"| {tag.LineNumber} | {MarkdownEscaper.TableCell(tag.Tag.Tag)} | {MarkdownEscaper.TableCell(tag.Tag.Value)} | {MarkdownEscaper.TableCell(tag.Tag.Message)} |");
        }
        sb.AppendLine();
    }

    private static void AppendStrategy(StringBuilder sb, MigrationStrategy strategy)
    {
        sb.AppendLine("## 移行戦略提案");
        sb.AppendLine();
        sb.AppendLine($"**判定**: {StrategyLabel(strategy)}");
        sb.AppendLine();
        sb.AppendLine(StrategyDescription(strategy));
        sb.AppendLine();
    }

    internal static IReadOnlyList<TaggedComment> ExtractTags(string source)
    {
        var tags = new List<TaggedComment>();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var tag = CommentTag.TryParse(lines[i]);
            if (tag is not null)
                tags.Add(new TaggedComment(i + 1, tag));
        }

        return tags;
    }

    internal static string StrategyLabel(MigrationStrategy strategy) => strategy switch
    {
        MigrationStrategy.BigBang => "ビッグバン移行",
        MigrationStrategy.Incremental => "段階的移行",
        MigrationStrategy.StranglerFig => "Strangler Fig パターン",
        MigrationStrategy.NeedsStudy => "詳細調査が必要",
        _ => strategy.ToString()
    };

    internal static string StrategyDescription(MigrationStrategy strategy) => strategy switch
    {
        MigrationStrategy.BigBang => "MDI スコアが低く、プログラム間依存も少ないため、ビッグバン移行が実現可能です。一括置換によるリスクは低いと判断されます。",
        MigrationStrategy.Incremental => "中程度の複雑性または依存関係を持つため、段階的な移行を推奨します。機能単位での順次移行を計画してください。",
        MigrationStrategy.StranglerFig => "高い複雑性または多くのプログラム間依存が存在します。Strangler Fig パターンによる段階的置換が適切です。継ぎ目となる境界を特定してから着手してください。",
        MigrationStrategy.NeedsStudy => "MDI スコアが Critical レベルです。構造的複雑性が非常に高く、移行前に詳細な調査が必要です。専門家によるレビューを推奨します。",
        _ => string.Empty
    };
}

internal record TaggedComment(int LineNumber, CommentTag Tag);
