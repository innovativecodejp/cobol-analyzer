namespace CobolAnalyzer.Engine.Export;

internal static class MarkdownEscaper
{
    public static string TableCell(string? value, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace("|", "\\|")
            .Trim();
    }
}
