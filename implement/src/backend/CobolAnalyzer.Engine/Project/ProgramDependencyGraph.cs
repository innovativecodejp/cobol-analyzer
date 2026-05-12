using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Metrics;

namespace CobolAnalyzer.Engine.Project;

public class ProgramDependencyGraph
{
    public List<DependencyNode> Nodes { get; init; } = new();
    public List<DependencyEdge> Edges { get; init; } = new();
    public bool HasCycle { get; init; }
    public bool HasDynamicCall { get; init; }
}

public class DependencyNode
{
    public string ProgramName { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public MdiScore? Mdi { get; init; }
    public bool IsExternal { get; init; }
    public int FanIn { get; init; }
    public int FanOut { get; init; }
}

public class DependencyEdge
{
    public string CallerProgram { get; init; } = string.Empty;
    public string CalleeProgram { get; init; } = string.Empty;
    public List<SourceLocation> CallSites { get; init; } = new();
}
