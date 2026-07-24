using System.Text;
using System.Text.RegularExpressions;
using CobolAnalyzer.Core.Models;

namespace CobolAnalyzer.Parser;

/// <summary>
/// 生の COBOL ソースを、既存 lexer/parser が受け付けられる正規化済み文字列へ変換する前処理器（仕様 §3）。
///
/// パイプライン（処理順は §3.6 で確定）:
///   §3.1 固定形式の正規化（連番欄除外・指標欄処理・73–80 無視・リテラル対応の継続連結）
///   §3.2 旧式 IDENTIFICATION 段落の除去
///   §3.3 COPY のテキスト展開（未解決/循環/深さ超過/REPLACING は警告＋無害化）
///   §3.4 EXEC CICS / EXEC SQL ブロックの no-op 縮約
///   §3.5 区切りカンマ（「カンマ + 直後空白/行末」）の空白正規化（EXEC 縮約の後段）
///
/// 方針:
///   - 例外を投げない（仕様 §6）。IO 失敗は未解決 COPY 警告に落とす。
///   - リテラル/擬似テキスト/注記の判定は単一の状態機械 <see cref="LiteralScanner"/> を共有し、
///     §3.1 のリテラル行継続（CBSTM03A 型）と §3.5 のカンマ保護の両方で使う（片方だけにしない）。
///   - 行番号対応は best-effort で保持する（除去行は空スロット化。§3.5 は同一長置換で桁位置も保存）。
/// </summary>
public sealed class CobolPreprocessor
{
    private readonly CobolPreprocessorOptions _options;

    public CobolPreprocessor(CobolPreprocessorOptions? options = null)
        => _options = options ?? new CobolPreprocessorOptions();

    /// <summary>前処理を適用し、正規化済みソースと警告を返す。</summary>
    public PreprocessResult Process(string source)
    {
        if (string.IsNullOrEmpty(source))
            return new PreprocessResult { Text = source ?? string.Empty };

        var warnings = new List<ParseWarning>();

        var lines = NormalizeFixedForm(source);                 // §3.1
        lines = RemoveObsoleteIdParagraphs(lines);              // §3.2
        lines = ExpandCopy(lines, 0,                            // §3.3
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), warnings);
        lines = ReduceExecBlocks(lines, warnings);             // §3.4
        lines = NormalizeSeparatorCommas(lines);               // §3.5

        var text = string.Join("\n", lines.Select(l => l.Text));
        return new PreprocessResult { Text = text, Warnings = warnings };
    }

    // ---------------------------------------------------------------------
    // 共有リテラル追跡スキャナ（§3.5「リテラル追跡の共有」）
    // ---------------------------------------------------------------------

    /// <summary>走査の途中状態（行継続を跨いで引き継ぐ）。</summary>
    internal struct ScanState
    {
        public bool InLiteral;
        public char QuoteChar;
        public bool InPseudoText;

        public static ScanState Start => default;
    }

    /// <summary>
    /// 非数字リテラル（<c>'…'</c> / <c>"…"</c>、二重引用符エスケープ対応）・擬似テキスト（<c>== … ==</c>）・
    /// 行内注記（リテラル外の <c>*&gt;</c> 以降）を 1 つの状態機械で追跡する。
    /// </summary>
    internal static class LiteralScanner
    {
        /// <summary>
        /// <paramref name="text"/> を <paramref name="start"/> 状態から走査し、
        /// 各文字位置が「保護対象（リテラル/擬似テキスト/注記の内部）」かのマスクと終端状態を返す。
        /// </summary>
        public static (bool[] Protected, ScanState End) Scan(string text, ScanState start)
        {
            var mask = new bool[text.Length];
            var state = start;
            var inComment = false;
            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];

                if (inComment)
                {
                    mask[i] = true;
                    i++;
                    continue;
                }

                if (state.InLiteral)
                {
                    mask[i] = true;
                    if (c == state.QuoteChar)
                    {
                        if (i + 1 < text.Length && text[i + 1] == state.QuoteChar)
                        {
                            mask[i + 1] = true; // 二重引用符エスケープ（リテラル継続）
                            i += 2;
                            continue;
                        }
                        state.InLiteral = false; // リテラル閉じ
                    }
                    i++;
                    continue;
                }

                if (state.InPseudoText)
                {
                    mask[i] = true;
                    if (c == '=' && i + 1 < text.Length && text[i + 1] == '=')
                    {
                        mask[i + 1] = true;
                        state.InPseudoText = false;
                        i += 2;
                        continue;
                    }
                    i++;
                    continue;
                }

                // 通常コード状態
                if (c is '\'' or '"')
                {
                    state.InLiteral = true;
                    state.QuoteChar = c;
                    mask[i] = true;
                    i++;
                    continue;
                }
                if (c == '=' && i + 1 < text.Length && text[i + 1] == '=')
                {
                    state.InPseudoText = true;
                    mask[i] = true;
                    mask[i + 1] = true;
                    i += 2;
                    continue;
                }
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '>')
                {
                    inComment = true; // 行内注記 *> 以降
                    mask[i] = true;
                    mask[i + 1] = true;
                    i += 2;
                    continue;
                }

                i++;
            }

            return (mask, state);
        }
    }

    // ---------------------------------------------------------------------
    // §3.1 固定形式の正規化
    // ---------------------------------------------------------------------

    /// <summary>
    /// 各物理行について col1–6 を除外し、col7（指標欄）を判定し、col8–72 を採用、col73 以降を無視する。
    /// コメント行・継続行・段落除去行は空文字スロットとして残し、原本行との対応を維持する。
    /// col7 '-' の継続はリテラル追跡を用い、リテラル継続時は継続行 Area B の再開クォートを除去して連結する。
    /// </summary>
    internal static List<PpLine> NormalizeFixedForm(string source)
    {
        var result = new List<PpLine>();
        var raw = SplitLines(source);

        for (var i = 0; i < raw.Count; i++)
        {
            var line = raw[i];
            var origin = i + 1;

            if (line.Length <= 6) // 連番のみ or 空行
            {
                result.Add(new PpLine(string.Empty, origin));
                continue;
            }

            var indicator = line[6]; // col7 (0-based 6)

            if (indicator is '*' or '/') // コメント行
            {
                result.Add(new PpLine(string.Empty, origin));
                continue;
            }

            var code = ExtractCodeArea(line);

            if (indicator == '-') // 継続行
            {
                var prev = result.FindLastIndex(p => p.Text.Length > 0);
                if (prev >= 0)
                {
                    result[prev] = result[prev] with { Text = JoinContinuation(result[prev].Text, code) };
                    result.Add(new PpLine(string.Empty, origin));
                }
                else
                {
                    result.Add(new PpLine(code, origin));
                }
                continue;
            }

            result.Add(new PpLine(code, origin)); // 通常行
        }

        return result;
    }

    /// <summary>
    /// 継続行を直前行へ連結する。直前行が開いたリテラルの途中で終わる場合は、
    /// 継続行 Area B の先頭引用符（再開クォート）を除去してから連結する（COBOL リテラル継続規則）。
    /// </summary>
    private static string JoinContinuation(string prev, string continuationCode)
    {
        var (_, end) = LiteralScanner.Scan(prev, ScanState.Start);
        var tail = continuationCode.TrimStart();

        if (end.InLiteral && tail.Length > 0 && tail[0] == end.QuoteChar)
            tail = tail.Substring(1); // 再開クォートを除去してリテラルを継続

        return prev + tail;
    }

    /// <summary>col8–72（1-based）＝ 0-based index 7..71 を切り出す。col73 以降は無視。</summary>
    private static string ExtractCodeArea(string line)
    {
        const int start = 7;   // col8
        const int maxLen = 65; // col8..col72 = 65 chars
        if (line.Length <= start) return string.Empty;
        var len = Math.Min(maxLen, line.Length - start);
        return line.Substring(start, len).TrimEnd();
    }

    private static List<string> SplitLines(string source)
    {
        var normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n').ToList();
    }

    // ---------------------------------------------------------------------
    // §3.2 旧式 IDENTIFICATION 段落の除去
    // ---------------------------------------------------------------------

    private static readonly Regex ObsoleteHeader = new(
        @"^(AUTHOR|INSTALLATION|DATE-WRITTEN|DATE-COMPILED|SECURITY)\s*\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DivisionOrSection = new(
        @"^[A-Za-z0-9$#@-]+\s+(DIVISION|SECTION)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IdParagraphHeader = new(
        @"^(PROGRAM-ID|AUTHOR|INSTALLATION|DATE-WRITTEN|DATE-COMPILED|SECURITY|REMARKS)\s*\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 旧式 ID 段落見出し（AUTHOR. 等）と、それに続く自由記述行を、
    /// 次の段落／DIVISION／SECTION 見出しに達するまで除去する（空行スロットで置換）。
    /// </summary>
    internal static List<PpLine> RemoveObsoleteIdParagraphs(List<PpLine> lines)
    {
        var result = new List<PpLine>();
        var i = 0;

        while (i < lines.Count)
        {
            var head = lines[i].Text.TrimStart();
            if (ObsoleteHeader.IsMatch(head))
            {
                result.Add(lines[i] with { Text = string.Empty }); // 見出し行を除去
                i++;
                while (i < lines.Count && !IsIdBoundary(lines[i].Text))
                {
                    result.Add(lines[i] with { Text = string.Empty }); // 自由記述行を除去
                    i++;
                }
                continue; // 境界行は次ループで通常処理
            }

            result.Add(lines[i]);
            i++;
        }

        return result;
    }

    private static bool IsIdBoundary(string text)
    {
        var t = text.TrimStart();
        if (t.Length == 0) return false;
        return DivisionOrSection.IsMatch(t) || IdParagraphHeader.IsMatch(t);
    }

    // ---------------------------------------------------------------------
    // §3.3 COPY のテキスト展開
    // ---------------------------------------------------------------------

    private static readonly Regex CopyStart = new(
        @"^\s*COPY\s+(?<m>'[^']*'|""[^""]*""|[A-Za-z0-9$#@_-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReplacingClause = new(
        @"\bREPLACING\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private List<PpLine> ExpandCopy(
        List<PpLine> lines, int depth, HashSet<string> stack, List<ParseWarning> warnings)
    {
        var result = new List<PpLine>();
        var i = 0;

        while (i < lines.Count)
        {
            var match = CopyStart.Match(lines[i].Text);
            if (!match.Success)
            {
                result.Add(lines[i]);
                i++;
                continue;
            }

            // COPY 文の終端（'.' を含む行）まで収集する
            var end = i;
            while (end < lines.Count && !lines[end].Text.Contains('.')) end++;
            if (end >= lines.Count) end = lines.Count - 1;

            var origin = lines[i].OriginLine;
            var statement = string.Join(" ",
                Enumerable.Range(i, end - i + 1).Select(k => lines[k].Text)).Trim();

            if (ReplacingClause.IsMatch(statement))
            {
                warnings.Add(new ParseWarning(origin, ParseWarningKind.CopyReplacingUnsupported,
                    $"COPY ... REPLACING は本フェーズ非対応のため無害化しました: {statement}"));
                AppendBlanks(result, lines, i, end);
                i = end + 1;
                continue;
            }

            var member = match.Groups["m"].Value.Trim('\'', '"');
            var resolved = ResolveCopybook(member);

            if (resolved is null)
            {
                warnings.Add(new ParseWarning(origin, ParseWarningKind.UnresolvedCopy,
                    $"COPY メンバを解決できませんでした: {member}"));
                AppendBlanks(result, lines, i, end);
            }
            else if (stack.Contains(member))
            {
                warnings.Add(new ParseWarning(origin, ParseWarningKind.CopyCycle,
                    $"COPY の循環参照を検出したため展開を停止しました: {member}"));
                AppendBlanks(result, lines, i, end);
            }
            else if (depth >= _options.MaxCopyDepth)
            {
                warnings.Add(new ParseWarning(origin, ParseWarningKind.CopyDepthExceeded,
                    $"COPY 入れ子の深さ上限に達したため展開を停止しました: {member}"));
                AppendBlanks(result, lines, i, end);
            }
            else if (TryReadFile(resolved, out var copybookText))
            {
                var expanded = NormalizeFixedForm(copybookText); // 置換内容も §3.1 正規化
                stack.Add(member);
                expanded = ExpandCopy(expanded, depth + 1, stack, warnings); // 入れ子は best-effort
                stack.Remove(member);
                result.AddRange(expanded);
            }
            else
            {
                warnings.Add(new ParseWarning(origin, ParseWarningKind.UnresolvedCopy,
                    $"COPY メンバを読み込めませんでした: {member}"));
                AppendBlanks(result, lines, i, end);
            }

            i = end + 1;
        }

        return result;
    }

    private static void AppendBlanks(List<PpLine> result, List<PpLine> src, int from, int to)
    {
        for (var k = from; k <= to && k < src.Count; k++)
            result.Add(src[k] with { Text = string.Empty });
    }

    private string? ResolveCopybook(string member)
    {
        foreach (var dir in _options.CopybookPaths)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var ext in _options.CopybookExtensions)
            {
                var target = member + ext;
                foreach (var file in files)
                {
                    if (string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
        }

        return null;
    }

    private static bool TryReadFile(string path, out string content)
    {
        try
        {
            content = File.ReadAllText(path);
            return true;
        }
        catch (IOException) { content = string.Empty; return false; }
        catch (UnauthorizedAccessException) { content = string.Empty; return false; }
    }

    // ---------------------------------------------------------------------
    // §3.4 EXEC CICS / EXEC SQL ブロックの縮約
    // ---------------------------------------------------------------------

    private static readonly Regex ExecStart = new(
        @"\bEXEC\s+(CICS|SQL|SQLIMS)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExecEnd = new(
        @"END-EXEC", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// EXEC CICS/SQL ... END-EXEC ブロック（行跨り可・'.' 有無両対応）を検出し、
    /// no-op 文 CONTINUE へ縮約する。END-EXEC 後の残余（'.' や END-IF 等）は保持する。
    /// </summary>
    internal static List<PpLine> ReduceExecBlocks(List<PpLine> lines, List<ParseWarning> warnings)
    {
        var result = new List<PpLine>();
        var i = 0;

        while (i < lines.Count)
        {
            var startMatch = ExecStart.Match(lines[i].Text);
            if (!startMatch.Success)
            {
                result.Add(lines[i]);
                i++;
                continue;
            }

            var origin = lines[i].OriginLine;

            var end = i;
            var foundEnd = false;
            while (end < lines.Count)
            {
                if (ExecEnd.IsMatch(lines[end].Text)) { foundEnd = true; break; }
                end++;
            }
            if (!foundEnd) end = i; // 対応する END-EXEC が無ければ当該行のみ縮約

            var prefix = lines[i].Text.Substring(0, startMatch.Index);
            var suffix = string.Empty;
            if (foundEnd)
            {
                var endText = lines[end].Text;
                var idx = endText.IndexOf("END-EXEC", StringComparison.OrdinalIgnoreCase);
                suffix = endText.Substring(idx + "END-EXEC".Length).Trim();
            }

            var reduced = new StringBuilder(prefix);
            reduced.Append("CONTINUE");
            if (suffix.Length > 0) reduced.Append(' ').Append(suffix);

            result.Add(lines[i] with { Text = reduced.ToString().TrimEnd() });
            for (var k = i + 1; k <= end && k < lines.Count; k++)
                result.Add(lines[k] with { Text = string.Empty });

            warnings.Add(new ParseWarning(origin, ParseWarningKind.ExecBlockReduced,
                "EXEC ブロックを no-op（CONTINUE）へ縮約しました"));

            i = end + 1;
        }

        return result;
    }

    // ---------------------------------------------------------------------
    // §3.5 区切りカンマの正規化（§3.4 の後段）
    // ---------------------------------------------------------------------

    /// <summary>
    /// 「カンマ + 直後空白（または行末終端）」のカンマ 1 文字を、空白 1 文字へ同一長置換する（桁位置保存）。
    /// リテラル/擬似テキスト/注記の内部（<see cref="LiteralScanner"/> の保護マスク）は対象外。
    /// PIC 挿入文字（<c>PIC ZZ,ZZ9</c>）や <c>DECIMAL-POINT IS COMMA</c> の小数点（<c>1,5</c>）は
    /// 「直後が空白でない」ため規則上自動的に非対象になる（PIC/文脈追跡は行わない）。
    /// 添字カンマ（<c>TBL(I,J)</c>）も直後が空白でないため非対象（§9）。
    /// </summary>
    internal static List<PpLine> NormalizeSeparatorCommas(List<PpLine> lines)
    {
        var result = new List<PpLine>(lines.Count);

        foreach (var line in lines)
        {
            var text = line.Text;
            if (text.IndexOf(',') < 0)
            {
                result.Add(line);
                continue;
            }

            var (mask, _) = LiteralScanner.Scan(text, ScanState.Start);
            char[]? buffer = null;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != ',' || mask[i]) continue;

                var isSeparator = i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]);
                if (!isSeparator) continue; // 直後が空白/行末でないカンマ（PIC・小数点・添字）は触らない

                buffer ??= text.ToCharArray();
                buffer[i] = ' '; // 同一長置換（桁位置保存）
            }

            result.Add(buffer is null ? line : line with { Text = new string(buffer) });
        }

        return result;
    }
}

/// <summary>前処理の結果。<see cref="Text"/> は正規化済みソース、<see cref="Warnings"/> は非致命的事象。</summary>
public sealed class PreprocessResult
{
    public string Text { get; init; } = string.Empty;
    public List<ParseWarning> Warnings { get; init; } = new();
}

/// <summary>前処理パイプライン内部で扱う 1 行（正規化後テキストと原本行番号）。</summary>
internal sealed record PpLine(string Text, int OriginLine);
