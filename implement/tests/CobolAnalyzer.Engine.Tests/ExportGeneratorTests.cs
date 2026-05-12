using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Export;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Engine.Project;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Engine.Tests;

public class ExportGeneratorTests
{
    [Fact]
    public void AnnotationReport_ContainsProgramName()
    {
        var report = CreateAnnotationGenerator().Generate("PROG-A.cbl", Source("PROG-A"));

        Assert.Contains("# COBOL 移行分析レポート：PROG-A", report);
    }

    [Fact]
    public void AnnotationReport_ContainsTagComments()
    {
        var source = Source("PROG-A", "      * [MDI:HIGH] needs | review");

        var report = CreateAnnotationGenerator().Generate("PROG-A.cbl", source);

        Assert.Contains("MDI", report);
        Assert.Contains("needs \\| review", report);
    }

    [Fact]
    public void AnnotationReport_NoTagComments_ShowsNone()
    {
        var report = CreateAnnotationGenerator().Generate("PROG-A.cbl", Source("PROG-A"));

        Assert.Contains("## タグ付きコメント一覧", report);
        Assert.Contains("なし", report);
    }

    [Fact]
    public void AnnotationReport_ParseError_ReturnsMarkdown()
    {
        var report = CreateAnnotationGenerator().Generate("bad.cbl", "not cobol");

        Assert.Contains("## 解析エラー", report);
    }

    [Fact]
    public void MigrationDesign_ContainsRankingTable()
    {
        var report = CreateDesignGenerator().Generate(new[]
        {
            new CobolSource("PROG-A.cbl", Source("PROG-A", callTarget: "PROG-B")),
            new CobolSource("PROG-B.cbl", Source("PROG-B"))
        });

        Assert.Contains("## 移行優先度ランキング", report);
        Assert.Contains("| 順位 | プログラム名 |", report);
    }

    [Fact]
    public void MigrationDesign_ContainsDependencySection()
    {
        var report = CreateDesignGenerator().Generate(new[]
        {
            new CobolSource("PROG-A.cbl", Source("PROG-A", callTarget: "PROG-B")),
            new CobolSource("PROG-B.cbl", Source("PROG-B"))
        });

        Assert.Contains("## プログラム間依存関係", report);
        Assert.Contains("PROG-A", report);
        Assert.Contains("PROG-B", report);
    }

    private static AnnotationReportGenerator CreateAnnotationGenerator()
        => new(CreateProjectAnalyzer());

    private static MigrationDesignGenerator CreateDesignGenerator()
        => new(CreateProjectAnalyzer());

    private static IProjectAnalyzer CreateProjectAnalyzer()
    {
        var weights = new MdiWeights();
        return new ProjectAnalyzer(
            new TestProjectSourceParser(),
            new CfgBuilder(),
            new DfgBuilder(),
            new MdiCalculator(weights),
            new CallGraphBuilder(),
            new MigrationRankingBuilder());
    }

    private static string Source(string programName, string? comment = null, string? callTarget = null)
    {
        var lines = new List<string>
        {
            "       IDENTIFICATION DIVISION.",
            $"       PROGRAM-ID. {programName}.",
            "       DATA DIVISION.",
            "       WORKING-STORAGE SECTION.",
            "       01 WS-X PIC 9.",
            "       PROCEDURE DIVISION."
        };

        if (comment is not null)
            lines.Add(comment);

        lines.Add("       MAIN-PARA.");
        if (callTarget is not null)
            lines.Add($"           CALL \"{callTarget}\".");
        lines.Add("           MOVE 1 TO WS-X.");
        lines.Add("           STOP RUN.");

        return string.Join(Environment.NewLine, lines);
    }

    private sealed class TestProjectSourceParser : IProjectSourceParser
    {
        private readonly CobolParserFacade _parser = new();

        public ParseResult Parse(string source) => _parser.Parse(source);
    }
}
