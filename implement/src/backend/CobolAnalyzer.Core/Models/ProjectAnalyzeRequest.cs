namespace CobolAnalyzer.Core.Models;

public class ProjectAnalyzeRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}
