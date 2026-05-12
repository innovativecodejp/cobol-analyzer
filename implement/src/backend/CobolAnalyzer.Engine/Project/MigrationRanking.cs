using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Metrics;

namespace CobolAnalyzer.Engine.Project;

public enum MigrationStrategy
{
    BigBang,
    Incremental,
    StranglerFig,
    NeedsStudy
}

public class MigrationRankingEntry
{
    public int Rank { get; init; }
    public string ProgramName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public MdiScore Mdi { get; init; } = new();
    public int LineCount { get; init; }
    public int ParagraphCount { get; init; }
    public int FanIn { get; init; }
    public int FanOut { get; init; }
    public MigrationStrategy Strategy { get; init; }
}

public class MigrationRanking
{
    public List<MigrationRankingEntry> Entries { get; init; } = new();
}

public class MigrationRankingBuilder
{
    public MigrationRanking Build(
        IReadOnlyList<AnalyzeResult> programs,
        ProgramDependencyGraph dependencyGraph,
        IReadOnlyDictionary<string, string> fileNames,
        IReadOnlyDictionary<string, int> lineCounts,
        IReadOnlyDictionary<string, int> paragraphCounts)
    {
        var nodes = dependencyGraph.Nodes.ToDictionary(
            n => n.ProgramName,
            StringComparer.OrdinalIgnoreCase);

        var ranked = programs
            .Where(p => p.Ast is not null && p.Metrics is not null)
            .Select(p =>
            {
                var programName = NormalizeProgramName(p.Metrics!.ProgramName);
                nodes.TryGetValue(programName, out var node);

                return new MigrationRankingEntry
                {
                    ProgramName = programName,
                    FileName = fileNames.GetValueOrDefault(programName, string.Empty),
                    Mdi = p.Metrics.Mdi,
                    LineCount = lineCounts.GetValueOrDefault(programName),
                    ParagraphCount = paragraphCounts.GetValueOrDefault(programName),
                    FanIn = node?.FanIn ?? 0,
                    FanOut = node?.FanOut ?? 0,
                    Strategy = DetermineStrategy(p.Metrics.Mdi.Score, node?.FanIn ?? 0, node?.FanOut ?? 0)
                };
            })
            .OrderByDescending(e => e.Mdi.Score)
            .ThenByDescending(e => e.FanIn)
            .ThenBy(e => e.ProgramName, StringComparer.OrdinalIgnoreCase)
            .Select((entry, index) => new MigrationRankingEntry
            {
                Rank = index + 1,
                ProgramName = entry.ProgramName,
                FileName = entry.FileName,
                Mdi = entry.Mdi,
                LineCount = entry.LineCount,
                ParagraphCount = entry.ParagraphCount,
                FanIn = entry.FanIn,
                FanOut = entry.FanOut,
                Strategy = entry.Strategy
            })
            .ToList();

        return new MigrationRanking { Entries = ranked };
    }

    public static MigrationStrategy DetermineStrategy(double mdiScore, int fanIn, int fanOut)
    {
        var fanTotal = fanIn + fanOut;

        if (mdiScore >= 75.0)
            return MigrationStrategy.NeedsStudy;
        if (mdiScore >= 50.0 || fanTotal >= 6)
            return MigrationStrategy.StranglerFig;
        if (fanTotal >= 3 || mdiScore >= 25.0)
            return MigrationStrategy.Incremental;
        return MigrationStrategy.BigBang;
    }

    public static int CountParagraphNodes(ProgramNode ast)
        => CountParagraphNodesRecursive(ast);

    private static int CountParagraphNodesRecursive(AstNode node)
    {
        var count = node is ParagraphNode && node.Category == NodeCategory.Unit ? 1 : 0;
        foreach (var child in node.Children)
            count += CountParagraphNodesRecursive(child);
        return count;
    }

    private static string NormalizeProgramName(string name)
        => name.Trim().ToUpperInvariant();
}
