namespace CobolAnalyzer.Core.Models;

public class CommentRemoveResult
{
    public string Source { get; init; } = "";
    public int RemovedCount { get; init; }
    public List<RemovedLine> RemovedLines { get; init; } = new();
    public string? PatternError { get; init; }
}

public record RemovedLine(int LineNumber, string Content);
