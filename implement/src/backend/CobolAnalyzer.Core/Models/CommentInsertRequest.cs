namespace CobolAnalyzer.Core.Models;

public class CommentInsertRequest
{
    public string? Source { get; init; }
    public List<InsertionSpec> Insertions { get; init; } = new();
}

public record InsertionSpec(
    int TargetLine,
    string Tag,
    string Value,
    string Message);
