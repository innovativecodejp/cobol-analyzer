namespace CobolAnalyzer.Core.Ast;

public class DivisionNode : AstNode
{
    public string Name { get; init; } = string.Empty;
    public DivisionNode() => Category = NodeCategory.Structure;
}
