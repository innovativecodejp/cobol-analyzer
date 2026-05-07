using CobolAnalyzer.Engine.Cfg;

namespace CobolAnalyzer.Engine.Metrics.Calculators;

public static class CyclomaticComplexityCalculator
{
    public static Dictionary<string, int> Calculate(ControlFlowGraph cfg)
    {
        var result = new Dictionary<string, int>();
        var paragraphs = cfg.Blocks
            .Where(b => b.ParagraphName != null)
            .Select(b => b.ParagraphName!)
            .Distinct();

        foreach (var para in paragraphs)
        {
            var paraBlockIds = cfg.Blocks
                .Where(b => b.ParagraphName == para)
                .Select(b => b.Id)
                .ToHashSet();

            int conditionalEdges = cfg.Edges.Count(e =>
                paraBlockIds.Contains(e.FromBlockId) &&
                e.Kind == CfgEdgeKind.ConditionalTrue);

            result[para] = conditionalEdges + 1;
        }
        return result;
    }
}
