namespace CobolAnalyzer.Core.Models;

public class ExportDesignRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}
