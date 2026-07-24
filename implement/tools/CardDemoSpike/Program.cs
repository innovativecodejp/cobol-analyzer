using System.Text;
using System.Text.RegularExpressions;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Parser;

// CardDemo パース耐性 再スパイク（仕様 Phase 7 §7-3,4）。
//
// 使い方:
//   dotnet run --project tools/CardDemoSpike -- <cblDir> <cpyDir> [outputMarkdown]
//
// <cblDir> 配下の *.cbl / *.CBL を、コピーブック検索パス <cpyDir> を渡した
// CobolParserFacade.Parse に通し、pass/fail 一覧・バッチ/CICS 別集計・
// エラーバケットを出力する（再現可能な測定ハーネス）。
//
// CardDemo 本体はリポジトリに含めない（測定時に取得。submodule 正式化は次フェーズ）。

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: CardDemoSpike <cblDir> <cpyDir> [outputMarkdown]");
    return 1;
}

var cblDir = args[0];
var cpyDir = args[1];
var outPath = args.Length >= 3 ? args[2] : null;

if (!Directory.Exists(cblDir))
{
    Console.Error.WriteLine($"cblDir not found: {cblDir}");
    return 1;
}

var facade = new CobolParserFacade(new CobolPreprocessorOptions
{
    CopybookPaths = Directory.Exists(cpyDir) ? new[] { cpyDir } : Array.Empty<string>(),
});

var files = Directory.EnumerateFiles(cblDir)
    .Where(f => f.EndsWith(".cbl", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
    .ToList();

var rows = new List<Row>();
foreach (var file in files)
{
    var name = Path.GetFileName(file);
    ParseResult result;
    try
    {
        result = facade.Parse(File.ReadAllText(file));
    }
    catch (Exception ex)
    {
        rows.Add(new Row(name, false, $"EXCEPTION: {ex.GetType().Name}", 0, 0));
        continue;
    }

    var firstError = result.Errors.Count > 0 ? result.Errors[0].Message : "";
    var unresolvedCopies = result.Warnings.Count(w => w.Kind == ParseWarningKind.UnresolvedCopy);
    var execReduced = result.Warnings.Count(w => w.Kind == ParseWarningKind.ExecBlockReduced);
    rows.Add(new Row(name, result.IsSuccess, firstError, unresolvedCopies, execReduced));
}

// バッチ = CB*, CICS = CO*（それ以外は Other）
static string Bucket(string name) =>
    name.StartsWith("CB", StringComparison.OrdinalIgnoreCase) ? "Batch(CB*)" :
    name.StartsWith("CO", StringComparison.OrdinalIgnoreCase) ? "CICS(CO*)" : "Other";

var batch = rows.Where(r => Bucket(r.Name) == "Batch(CB*)").ToList();
var cics = rows.Where(r => Bucket(r.Name) == "CICS(CO*)").ToList();
var other = rows.Where(r => Bucket(r.Name) == "Other").ToList();

// エラーバケット（具体値を伏せて分類）
static string ErrorBucket(string msg)
{
    if (string.IsNullOrEmpty(msg)) return "(none)";
    var m = Regex.Replace(msg, "'[^']*'", "'…'");
    m = Regex.Replace(m, @"\d+", "N");
    return m.Length > 80 ? m[..80] : m;
}

var errorBuckets = rows
    .Where(r => !r.Success)
    .GroupBy(r => ErrorBucket(r.FirstError))
    .OrderByDescending(g => g.Count())
    .ToList();

var sb = new StringBuilder();
void W(string s = "") => sb.AppendLine(s);

W("# CardDemo 再スパイク結果（前処理配線後）");
W();
W($"- 実行対象: `{cblDir}`");
W($"- コピーブック: `{cpyDir}`");
W($"- 対象ファイル数: {rows.Count}");
W();
W("## サマリ");
W();
W("| バケット | pass / total |");
W("|---|---|");
W($"| Batch(CB*) | {batch.Count(r => r.Success)} / {batch.Count} |");
W($"| CICS(CO*)  | {cics.Count(r => r.Success)} / {cics.Count} |");
W($"| Other      | {other.Count(r => r.Success)} / {other.Count} |");
W($"| **合計**   | **{rows.Count(r => r.Success)} / {rows.Count}** |");
W();
W("## エラーバケット（失敗のみ・先頭エラーを正規化）");
W();
if (errorBuckets.Count == 0)
{
    W("（失敗なし）");
}
else
{
    W("| 件数 | 正規化した先頭エラー |");
    W("|---|---|");
    foreach (var g in errorBuckets)
        W($"| {g.Count()} | `{g.Key}` |");
}
W();
W("## ファイル別");
W();
W("| ファイル | 結果 | 未解決COPY | EXEC縮約 | 先頭エラー |");
W("|---|---|---|---|---|");
foreach (var r in rows.OrderBy(r => Bucket(r.Name)).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
{
    var status = r.Success ? "pass" : "FAIL";
    var err = r.FirstError.Replace("|", "\\|");
    if (err.Length > 60) err = err[..60] + "…";
    W($"| {r.Name} | {status} | {r.UnresolvedCopies} | {r.ExecReduced} | {err} |");
}

var report = sb.ToString();
Console.WriteLine(report);

if (outPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, report);
    Console.Error.WriteLine($"written: {outPath}");
}

// 概況を stderr にも（CI 判定用）
Console.Error.WriteLine(
    $"BATCH {batch.Count(r => r.Success)}/{batch.Count}  " +
    $"CICS {cics.Count(r => r.Success)}/{cics.Count}  " +
    $"TOTAL {rows.Count(r => r.Success)}/{rows.Count}");
return 0;

internal record Row(string Name, bool Success, string FirstError, int UnresolvedCopies, int ExecReduced);
