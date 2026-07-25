using System.Text;
using System.Text.RegularExpressions;

namespace DemoPrecompute.Rendering;

/// <summary>
/// 生成物 Markdown（見出し・GFM テーブル・箇条書き・強調・区切り線・段落）を
/// 素直に HTML 断片へ変換する最小コンバータ。外部ライブラリ非依存・決定論的。
/// </summary>
internal static class Markdown
{
    public static string ToHtml(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            if (line.TrimEnd() == "---")
            {
                sb.Append("<hr>\n");
                i++;
                continue;
            }

            var heading = Regex.Match(line, @"^(#{1,4})\s+(.*)$");
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length;
                sb.Append($"<h{level}>{Inline(heading.Groups[2].Value)}</h{level}>\n");
                i++;
                continue;
            }

            // テーブル: ヘッダ行 + 区切り行 (|---|) が続く
            if (line.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                i = AppendTable(sb, lines, i);
                continue;
            }

            if (Regex.IsMatch(line, @"^\s*[-*]\s+"))
            {
                sb.Append("<ul>\n");
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*[-*]\s+"))
                {
                    var item = Regex.Replace(lines[i], @"^\s*[-*]\s+", "");
                    sb.Append($"<li>{Inline(item)}</li>\n");
                    i++;
                }
                sb.Append("</ul>\n");
                continue;
            }

            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            // 段落（連続する非空行を結合）
            var para = new StringBuilder();
            while (i < lines.Length && lines[i].Trim().Length > 0 &&
                   !Regex.IsMatch(lines[i], @"^(#{1,4})\s") && lines[i].TrimEnd() != "---" &&
                   !(lines[i].Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1])) &&
                   !Regex.IsMatch(lines[i], @"^\s*[-*]\s+"))
            {
                if (para.Length > 0) para.Append(' ');
                para.Append(lines[i].Trim());
                i++;
            }
            sb.Append($"<p>{Inline(para.ToString())}</p>\n");
        }

        return sb.ToString();
    }

    private static bool IsTableSeparator(string line)
        => Regex.IsMatch(line.Trim(), @"^\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)+\|?$");

    private static int AppendTable(StringBuilder sb, string[] lines, int i)
    {
        var header = SplitRow(lines[i]);
        i += 2; // skip header + separator

        sb.Append("<table>\n<thead><tr>");
        foreach (var cell in header)
            sb.Append($"<th>{Inline(cell)}</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        while (i < lines.Length && lines[i].Contains('|') && lines[i].Trim().Length > 0)
        {
            var cells = SplitRow(lines[i]);
            sb.Append("<tr>");
            foreach (var cell in cells)
                sb.Append($"<td>{Inline(cell)}</td>");
            sb.Append("</tr>\n");
            i++;
        }
        sb.Append("</tbody>\n</table>\n");
        return i;
    }

    private static string[] SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed[1..];
        if (trimmed.EndsWith("|")) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static string Inline(string text)
    {
        var s = Esc(text);
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"`(.+?)`", "<code>$1</code>");
        return s;
    }

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
