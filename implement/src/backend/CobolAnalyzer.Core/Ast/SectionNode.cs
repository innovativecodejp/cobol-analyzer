namespace CobolAnalyzer.Core.Ast;

public class SectionNode : AstNode
{
    public string Name { get; init; } = string.Empty;
    public SectionNode() => Category = NodeCategory.Unit;
}
