namespace CobolAnalyzer.Engine.Metrics;

public class MetricsResult
{
    public string ProgramName { get; init; } = string.Empty;
    public int CyclomaticComplexity { get; init; }
    public double GoToDensity { get; init; }
    public int AlterCount { get; init; }
    public int MaxNestingDepth { get; init; }
    public double RedefinesDensity { get; init; }
    public int CrossScopeDependencies { get; init; }
    public MdiScore Mdi { get; init; } = new();
    public Dictionary<string, int> CcPerParagraph { get; init; } = new();
}
