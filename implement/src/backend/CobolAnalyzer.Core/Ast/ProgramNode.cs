namespace CobolAnalyzer.Core.Ast;

public class ProgramNode : AstNode
{
    public string Name { get; init; } = string.Empty;
    public ProgramNode() => Category = NodeCategory.Structure;
}
