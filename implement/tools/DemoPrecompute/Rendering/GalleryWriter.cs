using System.Text;
using CobolAnalyzer.Engine.Project;
using DemoPrecompute.Output;

namespace DemoPrecompute.Rendering;

/// <summary>
/// デモ B（閲覧専用ギャラリー）の静的 HTML を <c>docs/gallery/</c> に書き出す（仕様 §5）。
/// 図・レポートは <c>../data/</c> を相対参照する（Pages サブパスで解決）。
/// </summary>
internal static class GalleryWriter
{
    private const string Css = """
    :root{color-scheme:light dark}
    *{box-sizing:border-box}
    body{margin:0;font-family:system-ui,-apple-system,"Segoe UI",sans-serif;line-height:1.6;color:#1c2330;background:#f5f7fa}
    header{background:#2c3e50;color:#fff;padding:1.2rem 1.5rem}
    header a{color:#9ec5ff}
    main{max-width:1080px;margin:0 auto;padding:1.5rem}
    h1{margin:.2rem 0}
    h2{border-bottom:2px solid #e1e6ee;padding-bottom:.3rem;margin-top:2rem}
    table{border-collapse:collapse;width:100%;background:#fff;margin:1rem 0;font-size:.92rem}
    th,td{border:1px solid #dce2ea;padding:.4rem .6rem;text-align:left}
    th{background:#eef2f7}
    tr:nth-child(even) td{background:#fafbfc}
    .fig{overflow-x:auto;background:#fff;border:1px solid #dce2ea;border-radius:6px;padding:.5rem;margin:1rem 0}
    .fig img{display:block}
    .badge{display:inline-block;padding:.1rem .5rem;border-radius:10px;color:#fff;font-size:.8rem}
    .Low{background:#27ae60}.Medium{background:#f39c12}.High{background:#e67e22}.Critical{background:#e74c3c}
    .attribution{font-size:.85rem;color:#5a6474;background:#eef2f7;border-radius:6px;padding:.8rem 1rem;margin-top:2rem}
    code{background:#eef2f7;padding:.1rem .3rem;border-radius:3px;font-size:.9em}
    .cards{display:flex;flex-wrap:wrap;gap:.6rem;margin:1rem 0}
    .card{background:#fff;border:1px solid #dce2ea;border-radius:6px;padding:.7rem 1rem;min-width:180px}
    .card a{font-weight:600;text-decoration:none;color:#2c3e50}
    footer{max-width:1080px;margin:0 auto;padding:1.5rem;color:#5a6474;font-size:.85rem}
    """;

    public static void WriteIndex(
        string galleryDir,
        ProjectAnalyzeResult project,
        Manifest manifest)
    {
        var targets = new HashSet<string>(
            manifest.Programs.Select(p => p.ProgramName), StringComparer.OrdinalIgnoreCase);

        var body = new StringBuilder();
        body.Append("<h2>プロジェクト概要</h2>\n");
        body.Append($"<p>対象コーパス <strong>{Esc(manifest.Corpus.Name)}</strong> の全 " +
                    $"{manifest.Selection.TotalPrograms} プログラムを解析し、移行困難度指数（MDI）で" +
                    $"ランキングした。深い成果物（注釈レポート＋AST/CFG/DFG 図）は上位 " +
                    $"{manifest.Selection.Count} 本（MDI 上位 {manifest.Selection.TopN} ＋ バケット代表）を掲載する。</p>\n");
        body.Append($"<p><a href=\"migration-design.html\">▶ プロジェクト移行設計書</a></p>\n");

        body.Append("<h2>プログラム間依存グラフ</h2>\n");
        body.Append("<div class=\"fig\"><img src=\"../data/figures/dependency-graph.svg\" " +
                    "alt=\"program dependency graph\"></div>\n");
        body.Append($"<p>循環依存: {(project.DependencyGraph.HasCycle ? "あり" : "なし")} / " +
                    $"動的CALL: {(project.DependencyGraph.HasDynamicCall ? "あり" : "なし")} / " +
                    $"CALL エッジ数: {project.DependencyGraph.Edges.Count}</p>\n");

        body.Append("<h2>デモ対象プログラム</h2>\n<div class=\"cards\">\n");
        foreach (var p in manifest.Programs)
            body.Append($"<div class=\"card\"><a href=\"{Esc(p.ProgramName)}.html\">{Esc(p.ProgramName)}</a>" +
                        $"<br><span class=\"badge {Esc(p.Risk)}\">{Esc(p.Risk)}</span> MDI {p.Mdi:F1}</div>\n");
        body.Append("</div>\n");

        body.Append("<h2>移行優先度ランキング（全 " + manifest.Selection.TotalPrograms + " 本）</h2>\n");
        body.Append("<table>\n<thead><tr><th>順位</th><th>プログラム</th><th>MDI</th><th>リスク</th>" +
                    "<th>ファンイン</th><th>ファンアウト</th><th>推奨戦略</th></tr></thead>\n<tbody>\n");
        foreach (var e in project.Ranking.Entries)
        {
            var name = targets.Contains(e.ProgramName)
                ? $"<a href=\"{Esc(e.ProgramName)}.html\">{Esc(e.ProgramName)}</a>"
                : Esc(e.ProgramName);
            body.Append($"<tr><td>{e.Rank}</td><td>{name}</td><td>{e.Mdi.Score:F1}</td>" +
                        $"<td><span class=\"badge {e.Mdi.Risk}\">{e.Mdi.Risk}</span></td>" +
                        $"<td>{e.FanIn}</td><td>{e.FanOut}</td><td>{e.Strategy}</td></tr>\n");
        }
        body.Append("</tbody>\n</table>\n");

        body.Append(Attribution(manifest.Corpus));

        File.WriteAllText(
            Path.Combine(galleryDir, "index.html"),
            Page($"{manifest.Corpus.Name} — ギャラリー", body.ToString(), backLink: "../index.html"));
    }

    public static void WriteProgramPage(string galleryDir, ProgramEntry entry, string annotationHtml, CorpusInfo corpus)
    {
        var body = new StringBuilder();
        body.Append($"<p><span class=\"badge {Esc(entry.Risk)}\">{Esc(entry.Risk)}</span> " +
                    $"MDI {entry.Mdi:F1} / 推奨戦略 <strong>{Esc(entry.Strategy)}</strong> / " +
                    $"ファンイン {entry.FanIn} ・ ファンアウト {entry.FanOut}</p>\n");

        body.Append("<h2>注釈レポート</h2>\n");
        body.Append(annotationHtml);

        body.Append("<h2>AST（構造概観）</h2>\n");
        body.Append($"<div class=\"fig\"><img src=\"../data/figures/{Esc(entry.ProgramName)}-ast.svg\" alt=\"AST\"></div>\n");
        body.Append("<h2>制御フローグラフ（CFG）</h2>\n");
        body.Append($"<div class=\"fig\"><img src=\"../data/figures/{Esc(entry.ProgramName)}-cfg.svg\" alt=\"CFG\"></div>\n");
        body.Append("<h2>データフローグラフ（DFG）</h2>\n");
        body.Append($"<div class=\"fig\"><img src=\"../data/figures/{Esc(entry.ProgramName)}-dfg.svg\" alt=\"DFG\"></div>\n");

        body.Append(Attribution(corpus));

        File.WriteAllText(
            Path.Combine(galleryDir, $"{entry.ProgramName}.html"),
            Page($"{entry.ProgramName} — 注釈レポート", body.ToString(), backLink: "index.html"));
    }

    public static void WriteDesignPage(string galleryDir, string designHtml, CorpusInfo corpus)
    {
        var body = new StringBuilder();
        body.Append(designHtml);
        body.Append(Attribution(corpus));
        File.WriteAllText(
            Path.Combine(galleryDir, "migration-design.html"),
            Page("移行設計書", body.ToString(), backLink: "index.html"));
    }

    private static string Attribution(CorpusInfo corpus) =>
        "<div class=\"attribution\">出典: " +
        $"<a href=\"{Esc(corpus.SourceUrl)}\">{Esc(corpus.Name)}</a>" +
        $"（<code>{Esc(corpus.License)}</code>, pinned <code>{Esc(corpus.PinnedCommit[..12])}</code>）。" +
        "本ギャラリーは事前計算された静的成果物であり、バックエンドを必要としない。</div>\n";

    private static string Page(string title, string body, string backLink) => $"""
    <!doctype html>
    <html lang="ja">
    <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
    <title>{Esc(title)}</title><style>{Css}</style></head>
    <body>
    <header><a href="{Esc(backLink)}">← 戻る</a><h1>{Esc(title)}</h1></header>
    <main>
    {body}
    </main>
    <footer>COBOL Analyzer — 解析ギャラリー（閲覧専用）</footer>
    </body></html>
    """;

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
