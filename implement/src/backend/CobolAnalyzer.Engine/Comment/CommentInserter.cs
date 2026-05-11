using CobolAnalyzer.Core.Models;

namespace CobolAnalyzer.Engine.Comment;

public class CommentInserter
{
    public CommentInsertResult Insert(string source, IReadOnlyList<InsertionSpec> insertions)
    {
        var newline = DetectNewline(source);
        var lines = SplitLines(source);
        var warnings = new List<CommentWarning>();

        foreach (var spec in insertions.OrderByDescending(i => i.TargetLine))
        {
            var tag = new CommentTag(spec.Tag, spec.Value, spec.Message);
            var commentLine = tag.ToCobolCommentLine();

            if (commentLine.Length > 72)
            {
                warnings.Add(new CommentWarning(
                    spec.TargetLine,
                    $"コメント行が72列を超えています（{commentLine.Length}列）"));
            }

            var index = spec.TargetLine > lines.Count
                ? lines.Count
                : spec.TargetLine - 1;
            lines.Insert(index, commentLine);
        }

        return new CommentInsertResult
        {
            Source = string.Join(newline, lines),
            InsertedCount = insertions.Count,
            Warnings = warnings
        };
    }

    private static string DetectNewline(string source)
        => source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static List<string> SplitLines(string source)
        => source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
}
