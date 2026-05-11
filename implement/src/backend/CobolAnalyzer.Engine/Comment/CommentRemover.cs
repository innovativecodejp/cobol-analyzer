using System.Text.RegularExpressions;
using CobolAnalyzer.Core.Models;

namespace CobolAnalyzer.Engine.Comment;

public class CommentRemover
{
    private readonly TimeSpan _matchTimeout;

    public CommentRemover()
        : this(TimeSpan.FromSeconds(1))
    {
    }

    public CommentRemover(TimeSpan matchTimeout)
    {
        _matchTimeout = matchTimeout;
    }

    public CommentRemoveResult Preview(string source, string pattern)
        => Process(source, pattern, remove: false);

    public CommentRemoveResult Remove(string source, string pattern)
        => Process(source, pattern, remove: true);

    private CommentRemoveResult Process(string source, string pattern, bool remove)
    {
        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.None, _matchTimeout);
        }
        catch (ArgumentException ex)
        {
            return Error(source, $"無効な正規表現: {ex.Message}");
        }

        var newline = DetectNewline(source);
        var lines = SplitLines(source);
        var removedLines = new List<RemovedLine>();
        var keptLines = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var shouldRemove = false;

            if (IsFixedFormatComment(line))
            {
                try
                {
                    shouldRemove = regex.IsMatch(line[7..]);
                }
                catch (RegexMatchTimeoutException ex)
                {
                    return Error(source, $"正規表現の評価がタイムアウトしました: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    return Error(source, $"無効な正規表現: {ex.Message}");
                }
            }

            if (shouldRemove)
            {
                removedLines.Add(new RemovedLine(i + 1, line));
                if (!remove)
                    keptLines.Add(line);
            }
            else
            {
                keptLines.Add(line);
            }
        }

        return new CommentRemoveResult
        {
            Source = remove ? string.Join(newline, keptLines) : source,
            RemovedCount = removedLines.Count,
            RemovedLines = removedLines
        };
    }

    private static CommentRemoveResult Error(string source, string patternError)
        => new()
        {
            Source = source,
            RemovedCount = 0,
            PatternError = patternError
        };

    private static bool IsFixedFormatComment(string line)
        => line.Length >= 7 && line[6] == '*';

    private static string DetectNewline(string source)
        => source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static List<string> SplitLines(string source)
        => source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
}
