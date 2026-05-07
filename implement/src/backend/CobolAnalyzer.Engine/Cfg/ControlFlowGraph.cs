namespace CobolAnalyzer.Engine.Cfg;

public class ControlFlowGraph
{
    public string ProgramName { get; init; } = string.Empty;
    public List<BasicBlock> Blocks { get; init; } = new();
    public List<CfgEdge> Edges { get; init; } = new();
    public string EntryBlockId { get; init; } = string.Empty;
    public List<string> ExitBlockIds { get; init; } = new();
    public bool HasAlter { get; init; }
    public bool HasRecursion { get; init; }
}
