using System.Text;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Project;

namespace CobolAnalyzer.Engine.Export;

public class MigrationDesignGenerator
{
    private readonly IProjectAnalyzer _projectAnalyzer;

    public MigrationDesignGenerator(IProjectAnalyzer projectAnalyzer)
    {
        _projectAnalyzer = projectAnalyzer;
    }

    public string Generate(IReadOnlyList<CobolSource> sources)
    {
        var result = _projectAnalyzer.Analyze(sources);
        var sourceByFile = sources.ToDictionary(s => s.FileName, s => s.Source, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("# COBOL 移行設計書");
        sb.AppendLine();
        sb.AppendLine($"生成日: {DateTime.Today:yyyy-MM-dd}");
        sb.AppendLine($"対象プログラム数: {sources.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        AppendRanking(sb, result);
        AppendDependencies(sb, result);
        AppendProgramSummaries(sb, result, sourceByFile);
        AppendErrors(sb, result);

        return sb.ToString();
    }

    private static void AppendRanking(StringBuilder sb, ProjectAnalyzeResult result)
    {
        sb.AppendLine("## 移行優先度ランキング");
        sb.AppendLine();

        if (result.Ranking.Entries.Count == 0)
        {
            sb.AppendLine("なし");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 順位 | プログラム名 | ファイル名 | MDI | リスク | ファンイン | ファンアウト | 推奨戦略 |");
        sb.AppendLine("|------|------------|-----------|-----|--------|----------|------------|--------|");
        foreach (var entry in result.Ranking.Entries)
        {
            sb.AppendLine(
                $"| {entry.Rank} | {MarkdownEscaper.TableCell(entry.ProgramName)} | {MarkdownEscaper.TableCell(entry.FileName)} | {entry.Mdi.Score:F1} | {entry.Mdi.Risk} | {entry.FanIn} | {entry.FanOut} | {entry.Strategy} |");
        }
        sb.AppendLine();
    }

    private static void AppendDependencies(StringBuilder sb, ProjectAnalyzeResult result)
    {
        var graph = result.DependencyGraph;

        sb.AppendLine("## プログラム間依存関係");
        sb.AppendLine();
        sb.AppendLine("- 総プログラム数: " + graph.Nodes.Count);
        sb.AppendLine("- CALL エッジ数: " + graph.Edges.Count);
        sb.AppendLine("- 循環依存: " + (graph.HasCycle ? "あり" : "なし"));
        sb.AppendLine("- 動的CALL（解析不能）: " + (graph.HasDynamicCall ? "あり" : "なし"));
        sb.AppendLine();
        sb.AppendLine("### 依存関係一覧");
        sb.AppendLine();

        if (graph.Edges.Count == 0)
        {
            sb.AppendLine("なし");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 呼び出し元 | 呼び出し先 | CALL箇所数 |");
        sb.AppendLine("|-----------|-----------|-----------|");
        foreach (var edge in graph.Edges)
        {
            sb.AppendLine(
                $"| {MarkdownEscaper.TableCell(edge.CallerProgram)} | {MarkdownEscaper.TableCell(edge.CalleeProgram)} | {edge.CallSites.Count} |");
        }
        sb.AppendLine();
    }

    private static void AppendProgramSummaries(
        StringBuilder sb,
        ProjectAnalyzeResult result,
        IReadOnlyDictionary<string, string> sourceByFile)
    {
        sb.AppendLine("## 各プログラム分析サマリー");
        sb.AppendLine();

        foreach (var entry in result.Ranking.Entries)
        {
            var program = result.Programs.FirstOrDefault(p =>
                p.Metrics?.ProgramName.Equals(entry.ProgramName, StringComparison.OrdinalIgnoreCase) == true);

            sb.AppendLine($"### {entry.ProgramName}");
            sb.AppendLine($"- **ファイル**: {entry.FileName}");
            sb.AppendLine($"- **MDI**: {entry.Mdi.Score:F1}（{entry.Mdi.Risk}）");
            sb.AppendLine($"- **推奨戦略**: {entry.Strategy}");
            sb.AppendLine($"- **行数**: {entry.LineCount} / **パラグラフ数**: {entry.ParagraphCount}");

            if (program?.Metrics is not null)
            {
                var metrics = program.Metrics;
                sb.AppendLine(
                    $"- **主要指標**: CC={metrics.CyclomaticComplexity}, GD={metrics.GoToDensity:F3}, AD={metrics.AlterCount}, ND={metrics.MaxNestingDepth}");
            }

            if (sourceByFile.TryGetValue(entry.FileName, out var source))
                AppendFirstTags(sb, source);

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }
    }

    private static void AppendFirstTags(StringBuilder sb, string source)
    {
        var tags = AnnotationReportGenerator.ExtractTags(source).Take(3).ToList();
        if (tags.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("タグ付きコメント:");
        foreach (var tag in tags)
        {
            sb.AppendLine(
                $"- 行 {tag.LineNumber}: [{MarkdownEscaper.TableCell(tag.Tag.Tag)}:{MarkdownEscaper.TableCell(tag.Tag.Value)}] {MarkdownEscaper.TableCell(tag.Tag.Message)}");
        }
    }

    private static void AppendErrors(StringBuilder sb, ProjectAnalyzeResult result)
    {
        var parseErrors = result.Programs
            .SelectMany(p => p.Errors)
            .Select(e => $"Line {e.Line}:{e.Column} {e.Message}")
            .Concat(result.Errors)
            .ToList();

        if (parseErrors.Count == 0)
            return;

        sb.AppendLine("## 解析時の注意");
        sb.AppendLine();
        foreach (var error in parseErrors)
            sb.AppendLine($"- {MarkdownEscaper.TableCell(error)}");
        sb.AppendLine();
    }
}
