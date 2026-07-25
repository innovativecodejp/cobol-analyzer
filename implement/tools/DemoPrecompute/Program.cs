using System.Text;
using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Core.Samples;
using CobolAnalyzer.Engine;
using CobolAnalyzer.Engine.Project;
using DemoPrecompute.Analysis;
using DemoPrecompute.Output;
using DemoPrecompute.Rendering;
using DemoPrecompute.Selection;

// Phase 9 事前計算パイプライン（仕様 §4）。
// 固定コーパス（レジストリ carddemo）を解析し、静的サイト（デモ B/C）が読む成果物を
// docs/ 配下へ決定論的に書き出す。バックエンド不要でホストできる状態にする。
//
//   dotnet run --project tools/DemoPrecompute                 … 既定（sample=carddemo, N=8, out=<repo>/docs）
//   dotnet run --project tools/DemoPrecompute -- --n 10 --out ./docs --sample carddemo

var options = PrecomputeOptions.Parse(args);

// ---- 配置の解決 ----
var samplesBase = SampleRegistry.LocateBaseDirectory(AppContext.BaseDirectory)
    ?? throw new DirectoryNotFoundException("samples/registry.json が見つかりません");
var implementDir = Directory.GetParent(samplesBase)!.FullName;       // samples/ の親 = implement/
var repoRoot = Directory.GetParent(implementDir)!.FullName;          // implement/ の親 = リポジトリ
var appsettingsPath = Path.Combine(implementDir, "src", "backend", "CobolAnalyzer.API", "appsettings.json");

var outDir = options.OutDir ?? Path.Combine(repoRoot, "docs");
var dataDir = Path.Combine(outDir, "data");
var galleryDir = Path.Combine(outDir, "gallery");
var logPath = options.LogPath
    ?? Path.Combine(implementDir, "log", "working", "2026-07-26_phase9-precompute-selection.md");

// ---- コーパス解決 ----
var registry = SampleRegistry.Load(samplesBase);
if (!registry.TryResolve(options.Sample, out var sample))
{
    Console.Error.WriteLine($"sample not registered: {options.Sample}");
    return 1;
}
if (!sample.Exists)
{
    Console.Error.WriteLine(
        $"cobolDir が存在しません（submodule 未取得の可能性）: {sample.CobolDirPath}\n" +
        "  git submodule update --init --recursive を実行してください。");
    return 1;
}

var copybookPaths = sample.CopybookPaths.Where(Directory.Exists).ToList();
var files = sample.EnumerateCobolFiles().OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
Console.WriteLine($"[precompute] sample={options.Sample} files={files.Count} copybooks={copybookPaths.Count}");

var sources = files
    .Select(f => new CobolSource(Path.GetFileName(f), File.ReadAllText(f)))
    .ToList();
var sourceByFile = sources.ToDictionary(s => s.FileName, s => s.Source, StringComparer.OrdinalIgnoreCase);

// ---- 解析 ----
var factory = AnalyzerFactory.Create(copybookPaths, appsettingsPath);
Console.WriteLine($"[precompute] MDI weights: {factory.WeightsSource}");

var project = factory.ProjectAnalyzer.Analyze(sources);
var resultByName = new Dictionary<string, AnalyzeResult>(StringComparer.OrdinalIgnoreCase);
foreach (var r in project.Programs)
{
    var name = r.Metrics?.ProgramName ?? (r.Ast as ProgramNode)?.Name;
    if (!string.IsNullOrWhiteSpace(name))
        resultByName[name.Trim().ToUpperInvariant()] = r;
}

// ---- 対象集合の選定（§3） ----
var selection = TargetSelector.Select(project.Ranking, options.TopN);
Console.WriteLine($"[precompute] target set = {selection.Selected.Count} program(s)");

// ---- コーパス帰属 ----
var corpus = new CorpusInfo(
    sample.Definition.Name,
    sample.Definition.Description ?? sample.Definition.Name,
    sample.Definition.License ?? "",
    sample.Definition.SourceUrl ?? "",
    sample.Definition.PinnedCommit ?? "");

// ---- データ書き出し ----
Directory.CreateDirectory(dataDir);
// project.json は demo C の ProjectPanel（dependencyGraph / ranking のみ参照）向け。
// programs（全31本の AnalyzeResult）は size 削減のため空配列にし、個別解析は
// programs/{NAME}.json で配る（schema は ProjectAnalyzeResult のまま・非 silent／ログ参照）。
var projectForSite = new ProjectAnalyzeResult
{
    Programs = new List<AnalyzeResult>(),
    DependencyGraph = project.DependencyGraph,
    Ranking = project.Ranking,
    Errors = project.Errors,
};
Json.Write(Path.Combine(dataDir, "project.json"), projectForSite);

var figuresDir = Path.Combine(dataDir, "figures");
Directory.CreateDirectory(figuresDir);
File.WriteAllText(Path.Combine(figuresDir, "dependency-graph.svg"),
    SvgRenderer.RenderDependencyGraph(project.DependencyGraph));

var programEntries = new List<ProgramEntry>();
foreach (var entry in selection.Selected)
{
    if (!sourceByFile.TryGetValue(entry.FileName, out var source))
    {
        Console.Error.WriteLine($"[precompute] source 未解決: {entry.ProgramName} ({entry.FileName})");
        continue;
    }
    resultByName.TryGetValue(entry.ProgramName, out var result);

    var pname = entry.ProgramName;
    Json.Write(Path.Combine(dataDir, "programs", $"{pname}.json"), result ?? new AnalyzeResult());
    WriteText(Path.Combine(dataDir, "sources", $"{pname}.cbl"), source);

    var report = factory.AnnotationReportGenerator.Generate(entry.FileName, source);
    WriteText(Path.Combine(dataDir, "reports", $"{pname}-annotation-report.md"), report);

    if (result?.Ast is ProgramNode ast)
        WriteText(Path.Combine(figuresDir, $"{pname}-ast.svg"), SvgRenderer.RenderAst(ast));
    if (result?.Cfg is not null)
        WriteText(Path.Combine(figuresDir, $"{pname}-cfg.svg"), SvgRenderer.RenderCfg(result.Cfg));
    if (result?.Dfg is not null)
        WriteText(Path.Combine(figuresDir, $"{pname}-dfg.svg"), SvgRenderer.RenderDfg(result.Dfg));

    programEntries.Add(new ProgramEntry(
        entry.Rank, pname, entry.FileName, entry.Mdi.Score, entry.Mdi.Risk.ToString(),
        entry.Strategy.ToString(), entry.FanIn, entry.FanOut,
        $"sources/{pname}.cbl", $"programs/{pname}.json",
        $"reports/{pname}-annotation-report.md",
        new FigurePaths($"figures/{pname}-ast.svg", $"figures/{pname}-cfg.svg", $"figures/{pname}-dfg.svg")));
}

// プロジェクト移行設計書（全プログラム横断）
var design = factory.MigrationDesignGenerator.Generate(sources);
WriteText(Path.Combine(dataDir, "migration-design.md"), design);

var manifest = new Manifest(
    corpus,
    new SelectionInfo(selection.TopN, programEntries.Count, project.Ranking.Entries.Count),
    programEntries,
    "migration-design.md");
Json.Write(Path.Combine(dataDir, "manifest.json"), manifest);

// ---- デモ B ギャラリー（§5） ----
Directory.CreateDirectory(galleryDir);
GalleryWriter.WriteIndex(galleryDir, project, manifest);
foreach (var entry in programEntries)
{
    var reportPath = Path.Combine(dataDir, "reports", $"{entry.ProgramName}-annotation-report.md");
    var annotationHtml = Markdown.ToHtml(File.ReadAllText(reportPath));
    GalleryWriter.WriteProgramPage(galleryDir, entry, annotationHtml, corpus);
}
GalleryWriter.WriteDesignPage(galleryDir, Markdown.ToHtml(design), corpus);

// ---- 選定ログ（決定論スナップショット。silent 打ち切り禁止） ----
WriteText(logPath, BuildSelectionLog(options, sample, files.Count, project, selection, factory.WeightsSource));

Console.WriteLine($"[precompute] data → {dataDir}");
Console.WriteLine($"[precompute] gallery → {galleryDir}");
Console.WriteLine($"[precompute] selection log → {logPath}");
Console.Error.WriteLine(
    $"SELECTED {selection.Selected.Count}  " +
    $"KEYS {string.Join(",", selection.Selected.Select(e => e.ProgramName))}");
return 0;

static void WriteText(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path, content);
}

static string BuildSelectionLog(
    PrecomputeOptions options, ResolvedSample sample, int fileCount,
    ProjectAnalyzeResult project, TargetSelection selection, string weightsSource)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Phase 9 事前計算：デモ対象集合の選定スナップショット");
    sb.AppendLine();
    sb.AppendLine($"- sample: `{sample.Definition.Name}` (pin `{sample.Definition.PinnedCommit}`)");
    sb.AppendLine($"- 解析対象ファイル数: {fileCount}");
    sb.AppendLine($"- ランキング本数: {project.Ranking.Entries.Count}");
    sb.AppendLine($"- MDI 重み: {weightsSource}");
    sb.AppendLine($"- N（MDI 上位）: {selection.TopN}");
    sb.AppendLine($"- 選定本数（和集合）: {selection.Selected.Count}");
    sb.AppendLine();
    sb.AppendLine("## MDI 上位 N");
    sb.AppendLine();
    sb.AppendLine("| rank | program | MDI | risk | strategy | fanIn | fanOut |");
    sb.AppendLine("|---|---|---|---|---|---|---|");
    foreach (var e in selection.TopEntries)
        sb.AppendLine($"| {e.Rank} | {e.ProgramName} | {e.Mdi.Score:F1} | {e.Mdi.Risk} | {e.Strategy} | {e.FanIn} | {e.FanOut} |");
    sb.AppendLine();
    sb.AppendLine("## バケット代表（CB*/CO*/CS*）");
    sb.AppendLine();
    sb.AppendLine("| bucket | program | 上位Nに追加 |");
    sb.AppendLine("|---|---|---|");
    foreach (var b in selection.BucketAdditions)
        sb.AppendLine($"| {b.Bucket}* | {b.ProgramName ?? "(該当なし)"} | {(b.AddedBeyondTopN ? "追加" : "既に上位N")} |");
    sb.AppendLine();
    sb.AppendLine("## 選定集合（和集合・ランク昇順）");
    sb.AppendLine();
    sb.AppendLine("| rank | program | MDI | strategy |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var e in selection.Selected)
        sb.AppendLine($"| {e.Rank} | {e.ProgramName} | {e.Mdi.Score:F1} | {e.Strategy} |");
    sb.AppendLine();
    sb.AppendLine("## 全ランキング");
    sb.AppendLine();
    sb.AppendLine("| rank | program | MDI | risk | strategy | fanIn | fanOut |");
    sb.AppendLine("|---|---|---|---|---|---|---|");
    foreach (var e in project.Ranking.Entries)
        sb.AppendLine($"| {e.Rank} | {e.ProgramName} | {e.Mdi.Score:F1} | {e.Mdi.Risk} | {e.Strategy} | {e.FanIn} | {e.FanOut} |");

    var maxMdi = project.Ranking.Entries.Count > 0 ? project.Ranking.Entries.Max(e => e.Mdi.Score) : 0;
    var riskDist = project.Ranking.Entries
        .GroupBy(e => e.Mdi.Risk)
        .OrderBy(g => g.Key)
        .Select(g => $"{g.Key}={g.Count()}");
    sb.AppendLine();
    sb.AppendLine("## 注記（silent 変更禁止・既定値と実測差）");
    sb.AppendLine();
    sb.AppendLine("- **DFG 重複 FILLER 修正**: 本フェーズで `DfgBuilder.ComputeImpactClosure` を重複 Id 耐性に修正した結果、");
    sb.AppendLine($"  解析可能プログラムが 10 → {project.Ranking.Entries.Count} 本に増加（`implement/docs/feedback-phase9-dfg-filler-duplicate-key.md`）。");
    sb.AppendLine($"- **MDI 分布**: {string.Join(" / ", riskDist)}（最大 MDI {maxMdi:F1}）。固定コーパス＋固定重みでは");
    sb.AppendLine("  High/Critical に達するプログラムは無く、戦略は BigBang / Incremental の範囲。重みは改変しない（仕様 §8）。");
    sb.AppendLine("- **project.json**: `programs` は空配列（size 削減）。demo C の ProjectPanel は dependencyGraph/ranking のみ参照し、");
    sb.AppendLine("  個別 AnalyzeResult は `programs/{NAME}.json` で配布。schema は ProjectAnalyzeResult のまま。");
    sb.AppendLine("- **AST 図**: 構造概観（Structure/Unit カテゴリ）。Element レベル（Statement/DataItem）は可読性のため省略。");
    sb.AppendLine("- **N / 図形式 / Pages ソース**: 既定（N=8 / SVG / docs/）。");
    return sb.ToString();
}

internal sealed record PrecomputeOptions(string Sample, int TopN, string? OutDir, string? LogPath)
{
    public static PrecomputeOptions Parse(string[] args)
    {
        var sample = "carddemo";
        var topN = 8;
        string? outDir = null;
        string? logPath = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--sample": sample = args[++i]; break;
                case "--n": if (int.TryParse(args[i + 1], out var n)) { topN = n; i++; } break;
                case "--out": outDir = args[++i]; break;
                case "--log": logPath = args[++i]; break;
            }
        }
        return new PrecomputeOptions(sample, topN, outDir, logPath);
    }
}
