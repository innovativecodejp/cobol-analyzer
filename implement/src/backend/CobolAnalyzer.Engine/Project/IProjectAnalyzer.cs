using CobolAnalyzer.Core.Models;

namespace CobolAnalyzer.Engine.Project;

public interface IProjectAnalyzer
{
    ProjectAnalyzeResult Analyze(IReadOnlyList<CobolSource> sources);
}
