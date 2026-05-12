using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Project;

namespace CobolAnalyzer.Engine.Tests;

public class MigrationRankingTests
{
    [Fact]
    public void Rank_ByMdiDescending()
    {
        var ranking = BuildRanking(
            Result("LOW", 10),
            Result("HIGH", 70));

        Assert.Equal("HIGH", ranking.Entries[0].ProgramName);
        Assert.Equal("LOW", ranking.Entries[1].ProgramName);
    }

    [Fact]
    public void Strategy_Critical_NeedsStudy()
    {
        Assert.Equal(
            MigrationStrategy.NeedsStudy,
            MigrationRankingBuilder.DetermineStrategy(75, 0, 0));
    }

    [Fact]
    public void Strategy_HighFanInOut_StranglerFig()
    {
        Assert.Equal(
            MigrationStrategy.StranglerFig,
            MigrationRankingBuilder.DetermineStrategy(10, 3, 3));
    }

    [Fact]
    public void Strategy_Low_BigBang()
    {
        Assert.Equal(
            MigrationStrategy.BigBang,
            MigrationRankingBuilder.DetermineStrategy(10, 1, 1));
    }

    [Fact]
    public void Rank_ParagraphCount_CountsParagraphNodes()
    {
        var ast = new ProgramNode
        {
            Name = "PROG-A",
            Children =
            {
                new DivisionNode
                {
                    Name = "PROCEDURE DIVISION",
                    Children =
                    {
                        new ParagraphNode { Name = "MAIN-PARA" },
                        new ParagraphNode { Name = "WORK-PARA" },
                        new StatementNode { StatementType = "DISPLAY" }
                    }
                }
            }
        };

        Assert.Equal(2, MigrationRankingBuilder.CountParagraphNodes(ast));
    }

    [Fact]
    public void Rank_TieBreaksByFanInThenProgramName()
    {
        var graph = new ProgramDependencyGraph
        {
            Nodes =
            {
                new DependencyNode { ProgramName = "PROG-A", FanIn = 1 },
                new DependencyNode { ProgramName = "PROG-B", FanIn = 2 },
                new DependencyNode { ProgramName = "PROG-C", FanIn = 1 }
            }
        };

        var ranking = new MigrationRankingBuilder().Build(
            new[] { Result("PROG-C", 30), Result("PROG-A", 30), Result("PROG-B", 30) },
            graph,
            new Dictionary<string, string>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>());

        Assert.Equal(new[] { "PROG-B", "PROG-A", "PROG-C" }, ranking.Entries.Select(e => e.ProgramName));
    }

    private static MigrationRanking BuildRanking(params AnalyzeResult[] results)
        => new MigrationRankingBuilder().Build(
            results,
            new ProgramDependencyGraph
            {
                Nodes = results.Select(r => new DependencyNode
                {
                    ProgramName = r.Metrics!.ProgramName
                }).ToList()
            },
            new Dictionary<string, string>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>());

    private static AnalyzeResult Result(string programName, double mdiScore)
        => new()
        {
            Ast = new ProgramNode { Name = programName },
            Metrics = new MetricsResult
            {
                ProgramName = programName,
                Mdi = new MdiScore
                {
                    Score = mdiScore,
                    Risk = mdiScore >= 75 ? MdiRisk.Critical :
                        mdiScore >= 50 ? MdiRisk.High :
                        mdiScore >= 25 ? MdiRisk.Medium :
                        MdiRisk.Low
                }
            }
        };
}
