namespace CobolAnalyzer.Engine.Dfg;

public class DataFlowGraph
{
    public string ProgramName { get; init; } = string.Empty;
    public List<DfgNode> Nodes { get; init; } = new();
    public List<DfgEdge> Edges { get; init; } = new();
    public Dictionary<string, List<string>> ImpactClosure { get; init; } = new();
}
