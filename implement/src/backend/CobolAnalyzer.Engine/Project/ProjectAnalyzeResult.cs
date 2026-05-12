namespace CobolAnalyzer.Engine.Project;

public class ProjectAnalyzeResult
{
    public List<AnalyzeResult> Programs { get; init; } = new();
    public ProgramDependencyGraph DependencyGraph { get; init; } = new();
    public MigrationRanking Ranking { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}
