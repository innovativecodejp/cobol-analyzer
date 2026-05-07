namespace CobolAnalyzer.Engine.Dfg;

public class DfgNode
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int LevelNumber { get; init; }
    public string? Picture { get; init; }
    public bool IsGroup { get; init; }
}
