using System.Text.RegularExpressions;

namespace CobolAnalyzer.Engine.Comment;

public record CommentTag(string Tag, string Value, string Message)
{
    private static readonly Regex Pattern = new(
        @"^\s{6}\*\s\[([A-Z0-9\-]+):([^\:\]]+)\]\s?(.*)$",
        RegexOptions.Compiled);

    public string ToCobolCommentLine()
        => $"      * [{Tag}:{Value}] {Message}";

    public static CommentTag? TryParse(string line)
    {
        var match = Pattern.Match(line);
        if (!match.Success)
            return null;

        return new CommentTag(
            match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Value);
    }
}
