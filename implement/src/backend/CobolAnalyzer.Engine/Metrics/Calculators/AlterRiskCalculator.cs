using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Metrics.Calculators;

public static class AlterRiskCalculator
{
    public static int Calculate(ProgramNode ast)
    {
        var procDiv = ast.Children.OfType<DivisionNode>()
            .FirstOrDefault(d => d.Name == "PROCEDURE DIVISION");
        if (procDiv == null) return 0;

        return CollectAllStatements(procDiv).Count(s => s.StatementType == "ALTER");
    }

    private static IEnumerable<StatementNode> CollectAllStatements(DivisionNode procDiv)
    {
        foreach (var child in procDiv.Children)
        {
            if (child is ParagraphNode para)
                foreach (var s in FlattenStatements(para.Children.OfType<StatementNode>()))
                    yield return s;
            else if (child is SectionNode section)
                foreach (var p in section.Children.OfType<ParagraphNode>())
                    foreach (var s in FlattenStatements(p.Children.OfType<StatementNode>()))
                        yield return s;
        }
    }

    private static IEnumerable<StatementNode> FlattenStatements(IEnumerable<StatementNode> stmts)
    {
        foreach (var s in stmts)
        {
            yield return s;
            foreach (var ts in s.TrueStatements) yield return ts;
            foreach (var fs in s.FalseStatements) yield return fs;
        }
    }
}
