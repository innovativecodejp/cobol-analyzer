using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Metrics.Calculators;

public static class NestingDepthCalculator
{
    public static int Calculate(ProgramNode ast)
    {
        var procDiv = ast.Children.OfType<DivisionNode>()
            .FirstOrDefault(d => d.Name == "PROCEDURE DIVISION");
        if (procDiv == null) return 0;

        int max = 0;
        foreach (var child in procDiv.Children)
        {
            if (child is ParagraphNode para)
                foreach (var stmt in para.Children.OfType<StatementNode>())
                    max = Math.Max(max, MaxDepth(stmt, 0));
            else if (child is SectionNode section)
                foreach (var p in section.Children.OfType<ParagraphNode>())
                    foreach (var stmt in p.Children.OfType<StatementNode>())
                        max = Math.Max(max, MaxDepth(stmt, 0));
        }
        return max;
    }

    private static int MaxDepth(StatementNode stmt, int currentDepth)
    {
        bool isControl = stmt.StatementType is "IF" or "EVALUATE" or "PERFORM" or "PERFORM_LOOP" or "PERFORM_THRU";
        int depth = isControl ? currentDepth + 1 : currentDepth;
        int max = depth;

        foreach (var child in stmt.TrueStatements)
            max = Math.Max(max, MaxDepth(child, depth));
        foreach (var child in stmt.FalseStatements)
            max = Math.Max(max, MaxDepth(child, depth));

        return max;
    }
}
