using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;

namespace CobolAnalyzer.Engine.Metrics.Calculators;

public static class CrossScopeDependencyCalculator
{
    public static int Calculate(DataFlowGraph dfg, ControlFlowGraph cfg)
    {
        // Build a map of line number → paragraph name from the CFG
        var lineToParagraph = BuildLineToParagraphMap(cfg);

        int count = 0;
        var defineEdges = dfg.Edges.Where(e => e.Kind == DfgEdgeKind.Define).ToList();
        var useEdges = dfg.Edges.Where(e => e.Kind == DfgEdgeKind.Use).ToList();

        foreach (var use in useEdges)
        {
            var usePara = GetParagraphFromRef(use.StatementRef, lineToParagraph);
            if (usePara == null) continue;

            // Find defines for the same data item
            foreach (var define in defineEdges.Where(d => d.FromId == use.FromId))
            {
                var definePara = GetParagraphFromRef(define.StatementRef, lineToParagraph);
                if (definePara != null && definePara != usePara)
                {
                    count++;
                    break; // Count once per use edge
                }
            }
        }
        return count;
    }

    private static Dictionary<int, string> BuildLineToParagraphMap(ControlFlowGraph cfg)
    {
        var map = new Dictionary<int, string>();
        foreach (var block in cfg.Blocks)
        {
            if (block.ParagraphName == null) continue;
            foreach (var stmt in block.Statements)
                if (stmt.Location != null)
                    for (int line = stmt.Location.StartLine; line <= stmt.Location.StopLine; line++)
                        map.TryAdd(line, block.ParagraphName);
        }
        return map;
    }

    private static string? GetParagraphFromRef(string? stmtRef, Dictionary<int, string> map)
    {
        if (stmtRef == null) return null;
        // stmtRef format: "Line:N"
        var parts = stmtRef.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int line))
            return map.TryGetValue(line, out var para) ? para : null;
        return null;
    }
}
