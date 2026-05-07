namespace CobolAnalyzer.Core.Ast;

public class ConditionNode : AstNode
{
    public string ConditionText { get; init; } = string.Empty;
    public List<DataReferenceNode> References { get; init; } = new();
    public ConditionNode() => Category = NodeCategory.Element;
}
