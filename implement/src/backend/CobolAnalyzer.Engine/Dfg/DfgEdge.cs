namespace CobolAnalyzer.Engine.Dfg;

public enum DfgEdgeKind
{
    Define,
    Use,
    Redefines,
    GroupOf
}

public class DfgEdge
{
    public string FromId { get; init; } = string.Empty;
    public string ToId { get; init; } = string.Empty;
    public DfgEdgeKind Kind { get; init; }
    public string? StatementRef { get; init; }
}
