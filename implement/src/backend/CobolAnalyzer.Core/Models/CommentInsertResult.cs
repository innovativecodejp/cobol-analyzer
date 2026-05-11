namespace CobolAnalyzer.Core.Models;

public class CommentInsertResult
{
    public string Source { get; init; } = "";
    public int InsertedCount { get; init; }
    public List<CommentWarning> Warnings { get; init; } = new();
}

public record CommentWarning(int Line, string Message);
