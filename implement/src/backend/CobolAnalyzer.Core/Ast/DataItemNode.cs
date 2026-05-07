namespace CobolAnalyzer.Core.Ast;

public class DataItemNode : AstNode
{
    public int LevelNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Picture { get; init; }
    public string? RedefinesTarget { get; init; }
    public string? Value { get; init; }
    public bool IsGroup => Picture == null && Children.OfType<DataItemNode>().Any();
    public DataItemNode() => Category = NodeCategory.Element;
}
