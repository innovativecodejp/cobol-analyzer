using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Cfg;

public class BasicBlock
{
    public string Id { get; init; } = string.Empty;
    public string? ParagraphName { get; init; }
    public List<StatementNode> Statements { get; init; } = new();
    public SourceLocation? Location { get; init; }
}
