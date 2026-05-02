namespace CobolAnalyzer.Core.Ast;

public class ParagraphNode : AstNode
{
    public string Name { get; init; } = string.Empty;
    public ParagraphNode() => Category = NodeCategory.Unit;
}
