namespace CobolAnalyzer.Core.Ast;

public enum ReferenceKind { Define, Use }

public class DataReferenceNode : AstNode
{
    public string DataName { get; init; } = string.Empty;
    public ReferenceKind Kind { get; init; }
    public DataReferenceNode() => Category = NodeCategory.Element;
}
