namespace CobolAnalyzer.Engine.Cfg;

public enum CfgEdgeKind
{
    FallThrough,
    ConditionalTrue,
    ConditionalFalse,
    GoTo,
    PerformCall,
    PerformReturn,
    PerformThruCall,
    PerformThruReturn
}

public class CfgEdge
{
    public string FromBlockId { get; init; } = string.Empty;
    public string ToBlockId { get; init; } = string.Empty;
    public CfgEdgeKind Kind { get; init; }
    public bool IsRecursive { get; init; }
}
