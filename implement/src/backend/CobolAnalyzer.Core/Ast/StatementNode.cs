namespace CobolAnalyzer.Core.Ast;

public class StatementNode : AstNode
{
    public string StatementType { get; init; } = string.Empty;
    public string? PerformFrom { get; init; }
    public string? PerformThru { get; init; }
    public string? IoVerb { get; init; }
    public string? FileName { get; init; }
    public List<DataReferenceNode> Operands { get; init; } = new();
    public PerformDetailsNode? PerformDetails { get; init; }
    public StatementNode() => Category = NodeCategory.Element;
}
